using System.Text.Json;
using Aurora.Api.Features.Routing;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Routing;
using Dapper;
using Npgsql;
int count=0;
void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;}
var id=Guid.NewGuid();
var eastern=TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
var pegasus=OrderDocumentImport.Parse("""
[{"tagNumber":"23683991","terminalCode":"DFW","localScheduledAt":"2026-11-02T07:00:00","localAppointmentFrom":"2026-11-02T07:00:00","localAppointmentTo":"2026-11-02T18:00:00","order":{"id":"23683991","workType":"Delivery","pieces":1,"weight":453.2}}]
""",eastern).Single();
Check(pegasus.Order.TerminalCode=="DFW" && pegasus.Order.Id=="23683991","Separate printed terminal survives numeric waybill conversion");
Check(pegasus.Order.ScheduledAt==DateTimeOffset.Parse("2026-11-02T07:00:00-05:00") && pegasus.Order.AppointmentTo==DateTimeOffset.Parse("2026-11-02T18:00:00-05:00"),"Printed November 2 delivery window remains November 2 across DST boundary");
pegasus.ChangeLocalSchedule(new DateTime(2026,11,3,8,0,0));pegasus.ApplyLocalSchedule(eastern);
Check(pegasus.Order.AppointmentFrom==DateTimeOffset.Parse("2026-11-03T08:00:00-05:00") && pegasus.Order.AppointmentTo==DateTimeOffset.Parse("2026-11-03T19:00:00-05:00"),"Review schedule edits move the extracted appointment window together");
var dateOnly=OrderDocumentImport.Parse("""[{"localScheduledAt":"2026-11-02T00:00:00","order":{}}]""",eastern).Single();
Check(TimeZoneInfo.ConvertTime(dateOnly.Order.ScheduledAt,eastern).Date==new DateTime(2026,11,2),"Date-only service dates do not shift to previous day");
var dimensions=new OrderDocumentDraft{DocumentType="Pegasus",PrintedTotalPieces=1,PrintedTotalWeightLb=453.2m,FreightLines=[new(){Pieces=1,WeightLb=453.2m,Length=48,Width=40,Height=50}]};
dimensions.ApplyFreightTotals();
Check(dimensions.Order.Cube==55.56m && dimensions.Order.Notes.Contains("inches assumed"),"Pegasus unlabeled dimensions use approved inches convention with visible provenance");
dimensions.DocumentType="Other";dimensions.Order.Cube=0;dimensions.ApplyFreightTotals();
Check(dimensions.Order.Cube==0,"Other forms do not inherit the Pegasus unit assumption");
dimensions.PrintedTotalCubeFt3=60;dimensions.ApplyFreightTotals();
Check(dimensions.Order.Cube==60,"Printed cube takes precedence over calculated dimensions");
var lineTotals=new OrderDocumentDraft{FreightLines=[new(){Pieces=2,WeightLb=10},new(){Pieces=3,WeightLb=20}]};lineTotals.ApplyFreightTotals();
Check(lineTotals.Order.Pieces==5 && lineTotals.Order.Weight==30,"Freight totals are summed from actual line data without multiplying total line weights");
var filenameDraft=new OrderDocumentDraft{TagNumber="23683991",Order=new(){Id="23683991",TerminalCode="DFW"}};
OrderDocumentImport.ApplyFilenameIdentity([filenameDraft],"LYKES0003714580B_2.TIF");
Check(filenameDraft.Order.Id=="3714580" && filenameDraft.Order.TerminalCode=="DFW" && filenameDraft.Order.Notes.Contains("23683991"),"Lykes filename supplies own PRO without losing carrier ID or separate terminal");
filenameDraft=new();OrderDocumentImport.ApplyFilenameIdentity([filenameDraft],"noproLYKES0003733336B_1.TIF");
Check(filenameDraft.Order.Id=="3733336","User-added nopro marker does not hide original Lykes filename PRO");
filenameDraft=new(){PrintedLykesTag="MCO42",TagNumber="MCO42",Order=new(){Id="42"}};OrderDocumentImport.ApplyFilenameIdentity([filenameDraft],"LYKES0003733336B_1.TIF");
Check(filenameDraft.Order.Id=="42","An explicitly printed Lykes Tag takes precedence over filename");
var storage = OrderDocumentImport.Parse("""[{"documentType":"Lykes","tagNumber":"TPA3735950","houseAirWaybill":"TPA3735950","order":{}}]""").Single();
OrderDocumentImport.ApplyFilenameIdentity([storage], "LYKES0003739303B_1.TIF");
Check(storage.Order.Id == "3735950" && storage.Order.TerminalCode == "TPA", "Lykes form header Tag outranks a conflicting filename");
var ait = OrderDocumentImport.Parse("""[{"documentType":"AIT","terminalCode":"TPA/X","order":{}}]""").Single();
OrderDocumentImport.ApplyFilenameIdentity([ait], "LYKES0003733051B_2.TIF");
Check(ait.Order.Id == "3733051" && ait.Order.TerminalCode == "TPA", "AIT delivery terminal maps to Tampa");
var forward = OrderDocumentImport.Parse("""[{"documentType":"ForwardAir","destinationTerminalCode":"AUS","order":{"workType":"Delivery","notes":"Waybill 97430543"}}]""").Single();
OrderDocumentImport.ApplyFilenameIdentity([forward], "LYKES0003736646C_1.TIF");
Check(forward.Order.Id == "3736646" && forward.Order.TerminalCode == "AUS" && forward.Order.Notes.Contains("97430543"), "Forward Air delivery retains carrier waybill and uses Lykes C-series PRO and destination");
var input=JsonSerializer.Serialize(new[]{new OrderDocumentDraft {ImportId=id,NativeStatusId=999,TagNumber="MCO3706592",ShipmentReference="SHIP-42",HouseAirWaybill="HAWB-42",MasterAirWaybill="MAWB-42",Order=new(){Id="wrong",Reference="invented-bol",Customer="Consignee",Address="123 Main",Pieces=4,Weight=500,SourceId=id,TerminalId=id,ManifestId=id,CustomerId=id,NativeOrderId=42,Status="Dispatched",Revision=12}}});
foreach(var json in new[]{input,JsonSerializer.Serialize(input),JsonSerializer.Serialize("```json\n"+input+"\n```")})
{
 var draft=OrderDocumentImport.Parse(json).Single();var order=draft.Order;
 Check(draft.ImportId is null && draft.NativeStatusId is null,"Extraction must not assign import history or status identity");
 Check(order.Id=="3706592" && order.TerminalCode=="MCO" && order.Weight==500 && order.Pieces==4 && order.Address=="123 Main","Business data lost");
 Check(order.Reference=="","Blank BOL filled from another identifier");
 Check(order.SourceId==Guid.Empty && order.TerminalId is null && order.ManifestId is null && order.CustomerId is null && order.NativeOrderId is null && order.Revision==0 && order.Status==WorkspaceOrderStatus.Ready,"Untrusted ownership/lifecycle leaked");
 Check(draft.NotesWithReferences().Contains("HAWB: HAWB-42") && draft.NotesWithReferences().Contains("Shipment reference: SHIP-42"),"Labeled identifiers lost");
 order.Validate();
}
// Expected identities manually verified against Mike's seven sample PDFs.
foreach(var (tag,pro,terminal) in new[]{("MCO3706592","3706592","MCO"),("DFW3696808","3696808","DFW"),("TPA3701517","3701517","TPA"),("AUS3698582","3698582","AUS"),("MCO3662120","3662120","MCO"),("IAH3704352","3704352","IAH"),("DFW3720309","3720309","DFW"),(" mco-0000042 ","42","MCO"),("42","42","")})
 Check(OrderDocumentDraft.SplitTag(tag)==(pro,terminal),"Incorrect Tag mapping: "+tag);
foreach(var invalid in new[]{"","0","-1","MCO0","MCO12X34","MCO1234567890","1.5","HAWB/123","\uff11\uff12\uff13"})
{
 try{OrderDocumentDraft.SplitTag(invalid);throw new Exception("Invalid Tag accepted");}catch(FormatException){count++;}
}
var explicitBol=new OrderDocumentDraft{TagNumber="TPA42",BillOfLading="BOL-17",ShipmentReference="REF-25"};explicitBol.Normalize();
Check(explicitBol.Order.Reference=="BOL-17","Explicit BOL not mapped");
foreach(var json in new[]{"[]","[null]","{}","not JSON",JsonSerializer.Serialize(Enumerable.Repeat(new OrderDocumentDraft(),101))})
{
 try{OrderDocumentImport.Parse(json);throw new Exception("Invalid response accepted: "+json);}
 catch(Exception ex) when(ex is FormatException or JsonException){count++;}
}
if(Environment.GetEnvironmentVariable("ORDER_IMPORT_TEST_CONNECTION") is {} raw)
{
 var cs=new NpgsqlConnectionStringBuilder(raw);
 if(cs.Host is not ("127.0.0.1" or "localhost") || !cs.Database!.StartsWith("aurora_import_check_"))throw new Exception("Disposable local import database required");
 await using var db=new NpgsqlConnection(raw);await db.OpenAsync();
 Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM pg_tables WHERE schemaname='public'")==0,"Test database must be empty");
 await db.ExecuteAsync("CREATE TABLE aurora_order(tenant_id uuid,source_id uuid,id text,deleted_at timestamptz)");
 var tenant=Guid.NewGuid();var other=Guid.NewGuid();
 await db.ExecuteAsync("INSERT INTO aurora_order VALUES(@tenant,@a,'00042',now()),(@tenant,@b,'77',NULL),(@other,@a,'99',NULL)",new{tenant,other,a=Guid.NewGuid(),b=Guid.NewGuid()});
 await using var ds=NpgsqlDataSource.Create(raw);
 var store=new TmsStore(new NpgsqlConnectionFactory(ds,new Context(tenant)),new Context(tenant));
 var found=await store.ExistingOrderNumbers(["42","77","99","88"],default);
 Check(found.Order().SequenceEqual(new[]{"42","77"}),"Duplicate lookup must include deleted/all sources and exclude other tenants");
 try{await store.SaveOrder(new(){Id="42",Customer="Test"},true,default);throw new Exception("Duplicate save accepted");}
 catch(FormatException ex){Check(ex.Message.Contains("already exists"),"Duplicate rejected before create");}
 Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_order")==3,"Duplicate save changed data");
}
var nativeTenant=new Context(Guid.NewGuid());
Check(NovaDocumentImport.PostalCode("32801-1234")=="328011234" && NovaDocumentImport.PostalCode("78754 4716")=="787544716" && NovaDocumentImport.PostalCode("K1A 0B1")=="K1A 0B1","ZIP+4 retains all digits within Nova nine-character field; other postal formats are preserved");
var nativeSource=new Aurora.Integrations.Nova.FirebirdRoutingSource(null!,nativeTenant,Microsoft.Extensions.Options.Options.Create(new Aurora.Integrations.Nova.FirebirdRoutingOptions{Enabled=true,TenantId=nativeTenant.TenantIdOrNull!.Value,SourceKey="test",ConnectionString="test"}));
var nativeDraft=new OrderDocumentDraft{TagNumber="MCO003706592",ShipmentReference="SHIP",HouseAirWaybill="HAWB",Order=new(){TerminalCode="MCO",Customer="Receiver",Shipper="Sender",Pieces=12,Weight=125,Cube=2,Notes="Freight: shelving",ScheduledAt=DateTimeOffset.Parse("2026-10-15T11:00:00-04:00")}};
var native=NovaDocumentImport.Build(nativeDraft,nativeSource.NewRecord("orders"),110,TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));
Check(native.Values["header"]["PBNUM"]=="3706592","Native import strips terminal prefix and leading zeroes");
Check(native.Values["header"]["PBBOL"]==""&&native.Values["delivery"]["OPS_BOL"]=="","Blank BOL is never borrowed from shipment or air waybill");
Check(native.Values["header"]["PBTPIECE"]=="12"&&native.Values["header"]["PBTWEIGH"]=="125","Native freight totals preserved");
Check(native.Values["pickup"]["OPS_NAME"]=="Sender"&&native.Values["delivery"]["OPS_NAME"]=="Receiver","Native pickup and delivery remain separate");
Check(native.Values["delivery"]["OPS_APPT_LOW"]=="2026-10-15T11:00:00"&&native.Values["pickup"]["OPS_APPT_LOW"] is null,"Service date uses native local time on active delivery stop");
Check(nativeDraft.NotesWithReferences().Contains("HAWB: HAWB")&&nativeDraft.NotesWithReferences().Contains("Freight: shelving"),"Native notes retain descriptions and distinct references");
nativeDraft.Order.WorkType="Pickup";
native=NovaDocumentImport.Build(nativeDraft,nativeSource.NewRecord("orders"),111,TimeZoneInfo.Utc);
Check(native.Values["header"]["PB_STATUS"]=="111"&&native.Values["pickup"]["OPS_APPT_LOW"]=="2026-10-15T15:00:00","Pickup uses selected status and pickup appointment");
nativeDraft.Order.TerminalCode="DFW";
native=NovaDocumentImport.Build(nativeDraft,nativeSource.NewRecord("orders"),NovaDocumentImport.DefaultStatus,TimeZoneInfo.Utc);
Check(native.Values["header"]["PB_LOCATION"]=="DFW","Selected terminal takes precedence over document Tag prefix");
var sparse=new OrderDocumentDraft{TagNumber="MCO42",Order=new(){Customer="",Shipper="",TerminalCode=null,WorkType="Pickup",Latitude=123,Longitude=null,ServiceSeconds=-1,ServiceType="unconfigured",Notes=new string('x',4100)}};
native=NovaDocumentImport.Build(sparse,nativeSource.NewRecord("orders"),NovaDocumentImport.DefaultStatus,TimeZoneInfo.Utc);
Check(native.Values["header"]["PB_STATUS"]=="110","Pickup import uses normal new-order status without planning configuration");
Check(string.IsNullOrEmpty(native.Values["header"]["PB_LOCATION"]),"Import allows an unassigned terminal");
Check(native.Values["pickup"]["OPS_NAME"]=="" && native.Values["delivery"]["OPS_NAME"]=="","Import allows missing shipper and consignee names");
Check(native.Values["pickup"]["OPS_APPT_LOW"] is not null,"Pickup work type still maps its appointment to pickup stop");
Check(sparse.NotesWithReferences().Length>4000,"Import does not require optimization fields or impose a combined notes limit");
native=NovaDocumentImport.Build(sparse,nativeSource.NewRecord("orders"),321,TimeZoneInfo.Utc);
Check(native.Values["header"]["PB_STATUS"]=="321","Import honors chosen Nova status independently of work type");
Check(nativeSource.NewRecord("orders").Parts.Single(p=>p.Key=="header").Fields.Single(f=>f.Key=="PB_STATUS").Value==NovaDocumentImport.DefaultStatus.ToString(),"Manual and imported orders share the same default status");
// Reopening an imported order uses its current values and preserves fields outside the compact form.
var editDraft=new OrderDocumentDraft{TagNumber="MCO456",ShipmentReference="REF-1",HouseAirWaybill="HAWB-1",MasterAirWaybill="MAWB-1",Order=new(){Id="456",TerminalCode="MCO",Customer="Current consignee",Shipper="Current shipper",WorkType="Pickup",ScheduledAt=DateTimeOffset.Parse("2026-11-04T12:00:00Z"),Notes="Operator notes"}};
var editTemplate=nativeSource.NewRecord("orders");
var editable=NovaDocumentImport.Build(editDraft,editTemplate,110,TimeZoneInfo.Utc);
editable.Values["pickup"]["OPS_CONTACT"]="Keep contact";
editable.Values["pickup"]["OPS_ADDR3"]="Keep address 3";
editable.Values["pickup"]["OPS_BOL"]="Pickup-specific BOL";
editable.Values["header"]["PBPURORD"]="Keep purchase order";
editable.Values["pickup"]["OPS_APPT_HIGH"]="2026-11-04T14:00:00";
editable.Values["delivery"]["OPS_APPT_LOW"]="2026-11-05T12:00:00";
editable.Values["delivery"]["OPS_APPT_HIGH"]="2026-11-05T16:00:00";
var savedRecord=new NovaRecordDocument(100,"Order","revision",editTemplate.Parts.Select(p=>p with{Key=p.Key=="header"?"header":p.Key=="pickup"?"stop-1":"stop-2",Fields=p.Fields.Select(f=>f with{Value=editable.Values[p.Key][f.Key]}).ToArray()}).ToArray());
var storedNotes=editDraft.NotesWithReferences();
var reopened=ImportedOrderMapping.Read(savedRecord,storedNotes,TimeZoneInfo.Utc);
Check(reopened.Order.Id=="456" && reopened.Order.Customer=="Current consignee" && reopened.NativeStatusId==110,"Reopened order maps live native data");
Check(reopened.ShipmentReference=="REF-1" && reopened.HouseAirWaybill=="HAWB-1" && reopened.MasterAirWaybill=="MAWB-1" && reopened.Order.Notes=="Operator notes","Saved reference metadata separates cleanly from editable notes");
Check(reopened.Order.WorkType=="Pickup" && reopened.Order.ScheduledAt==DateTimeOffset.Parse("2026-11-04T12:00:00Z"),"Stored work type selects the correct schedule even when both stops have appointments");
reopened.Order.Customer="Updated consignee";
var update=ImportedOrderMapping.Edit(savedRecord,reopened,TimeZoneInfo.Utc,storedNotes);
Check(update["stop-2"]["OPS_NAME"]=="Updated consignee","Review edit maps back to existing delivery stop");
Check(update["stop-1"]["OPS_BOL"]=="Pickup-specific BOL","Unedited stop-specific BOL survives compact editing");
Check(update["stop-1"]["OPS_CONTACT"]=="Keep contact" && update["stop-1"]["OPS_ADDR3"]=="Keep address 3" && update["header"]["PBPURORD"]=="Keep purchase order","Review saves preserve unexposed native fields");
Check(update["stop-1"]["OPS_APPT_HIGH"]=="2026-11-04T14:00:00" && update["stop-2"]["OPS_APPT_HIGH"]=="2026-11-05T16:00:00","Unedited appointment windows remain unchanged");
reopened.Order.ScheduledAt=reopened.Order.ScheduledAt.AddHours(1);
update=ImportedOrderMapping.Edit(savedRecord,reopened,TimeZoneInfo.Utc,storedNotes);
Check(update["stop-1"]["OPS_APPT_LOW"]=="2026-11-04T13:00:00" && update["stop-1"]["OPS_APPT_HIGH"]=="2026-11-04T15:00:00","Changing schedule preserves appointment duration");
update=ImportedOrderMapping.Edit(savedRecord,reopened,TimeZoneInfo.Utc,storedNotes,true);
Check(string.IsNullOrEmpty(update["stop-1"]["OPS_APPT_LOW"]) && update["stop-2"]["OPS_APPT_HIGH"]=="2026-11-05T16:00:00","Clearing schedule changes only the active stop");
try{ImportedOrderMapping.Parts(savedRecord with {Parts=[..savedRecord.Parts,savedRecord.Parts[1] with{Key="stop-3"}]});throw new Exception("Ambiguous stop mapping accepted");}
catch(FormatException){Check(true,"Expanded stop lists cannot be silently collapsed by compact editing");}
Console.WriteLine($"{count} order document import checks passed.");
var novaAddress=new NovaImportAddress{Code="AC1",Name="Existing consignee",Address="100 Nova Street",Address2="Suite 2",City="Orlando",State="FL",Zip="32801",Country="US"};
var addressDraft=new OrderDocumentDraft{Order=new(){Customer=" existing consignee ",City="ORLANDO",State="fl",PostalCode="32801",Address="Extracted street",BillTo="Unmatched payer"}};
var addressMatches=NovaImportAddress.Apply(addressDraft,[novaAddress]);
Check(addressMatches.ContainsKey("delivery") && addressDraft.Order.Address=="100 Nova Street" && addressDraft.Order.Address2=="Suite 2","Unique Nova match replaces extracted address");
Check(addressDraft.Order.BillTo=="Unmatched payer" && addressDraft.AddressLookupMessages.Any(m=>m.Contains("no unique")),"No match preserves imported values and reports lookup outcome");
Check(NovaImportAddress.Match([novaAddress],"Existing consignee","Miami","FL","")==null,"Different city must not select another branch");
Check(NovaImportAddress.Match([novaAddress,new NovaImportAddress{Code="AC2",Name=novaAddress.Name,Address="Other street",City="Orlando",State="FL",Zip="32801"}],novaAddress.Name,"Orlando","FL","32801")==null,"Ambiguous accounts are not guessed");
Check(NovaImportAddress.Match([novaAddress],"Unknown name","Orlando","FL","32801")==null,"Matching locality alone cannot select an unrelated account");
var sparseAddress=new OrderDocumentDraft{Order=new(){Shipper=novaAddress.Name,BillTo=novaAddress.Name}};
NovaImportAddress.Apply(sparseAddress,[novaAddress]);
Check(sparseAddress.Order.OriginAddress==novaAddress.Address && sparseAddress.Order.BillToAddress==novaAddress.Address,"Unique named account fills missing shipper and bill-to addresses");
Console.WriteLine($"{count} total import and Nova address checks passed.");
if(Environment.GetEnvironmentVariable("DANIELLE_EXTRACTION_CHECK_DIR") is {} extractionDir)
{
 var p=OrderDocumentImport.Parse(File.ReadAllText(Path.Combine(extractionDir,"LYKES0003714580B_2.TIF.updated.json")),eastern);
 OrderDocumentImport.ApplyFilenameIdentity(p,"LYKES0003714580B_2.TIF");
 Check(p.Single().Order.Id=="3714580" && p[0].Order.TerminalCode=="DFW" && p[0].Order.Cube==55.56m,"Actual Pegasus TIF extraction yields approved PRO, terminal and cube");
 Check(p[0].Order.Pieces==1 && p[0].Order.Weight==453.2m && TimeZoneInfo.ConvertTime(p[0].Order.ScheduledAt,eastern).DateTime==new DateTime(2026,11,2,7,0,0),"Actual Pegasus freight and delivery start match bill");
 var n=OrderDocumentImport.Parse(File.ReadAllText(Path.Combine(extractionDir,"noproLYKES0003733336B_1.TIF.updated.json")),eastern);
 OrderDocumentImport.ApplyFilenameIdentity(n,"noproLYKES0003733336B_1.TIF");
 Check(n[0].Order.Id=="3733336" && n[0].Order.Notes.Contains("USXI-353471"),"Actual Expeditors scan uses Lykes filename PRO and retains carrier identifier");
 Check(n[0].Order.Pieces==28 && n[0].Order.Notes.Contains("page 2 not supplied"),"Actual partial scan sums supplied line quantities and flags missing page");
 var mapped=NovaDocumentImport.Build(p[0],nativeSource.NewRecord("orders"),110,eastern);
 Check(mapped.Values["header"]["PB_LOCATION"]=="DFW" && mapped.Values["header"]["PB_TTL_CUBE"]=="55.56" && mapped.Values["delivery"]["OPS_APPT_LOW"]=="2026-11-02T07:00:00" && mapped.Values["delivery"]["OPS_APPT_HIGH"]=="2026-11-02T18:00:00","Corrected extraction survives native save mapping");
 Console.WriteLine($"{count} checks including actual Danielle TIFF extractions passed.");
}
if(Environment.GetEnvironmentVariable("CROWN_REPORT_CHECK") is {} crownPath)
{
 using var crownFile=File.OpenRead(crownPath);
 var crown=CrownOrderReport.Read(crownFile,TimeZoneInfo.FindSystemTimeZoneById("America/New_York"))!;
 Check(crown.Count==1797 && crown.Select(d=>d.TagNumber).Distinct().Count()==1797,"Crown report groups all 2128 rows into 1797 unique full Tags");
 Check(crown.Select(d=>d.Order.Id).Distinct().Count()==1797 && crown.Count(d=>d.Order.Notes.StartsWith("Crown Tag:"))==12,"Twelve shipments sharing numeric Tag suffixes use distinct Ship IDs and preserve original Tags");
 Check(crown.Single(d=>d.Order.Id=="3890166").Order.Notes.Contains("Crown Tag: SAT3727703"),"SAT conflicting Tag maps to its exact Crown Ship ID");
 Check(crown.Sum(d=>d.Order.Pieces)==7843 && crown.Sum(d=>d.Order.Weight)==1223789,"Crown repeated charge rows do not multiply pieces or actual weight");
 var first=crown.Single(d=>d.TagNumber=="IAH3726920");
 Check(first.Order.Id=="3726920" && first.Order.TerminalCode=="IAH" && first.Order.Pieces==2 && first.Order.Weight==743,"Crown screenshot shipment reconciles");
 Check(first.Order.ScheduledAt==DateTimeOffset.Parse("2026-10-26T00:00:00-04:00"),"Crown service date is converted from Excel serial in the source zone");
 Check(crown.All(d=>d.Order.Address=="" && d.Order.OriginAddress=="" && d.BillOfLading==""),"Crown missing street addresses and BOLs stay blank before Nova lookup");
 Check(first.HouseAirWaybill=="" && first.MasterAirWaybill=="","Crown dot placeholders remain blank");
 var crownTemplate=nativeSource.NewRecord("orders");
 Check(crown.All(d=>{var mapped=NovaDocumentImport.Build(d,crownTemplate,110,TimeZoneInfo.Utc);return crownTemplate.Parts.All(p=>p.Fields.Where(f=>f.Kind=="text").All(f=>(mapped.Values[p.Key][f.Key]?.Length??0)<=f.MaxLength));}),"Every Crown shipment fits Nova text field lengths after mapping");
 Console.WriteLine($"{count} total checks including actual Crown workbook passed.");
}
sealed class Context(Guid id):ITenantContext{public Guid? TenantIdOrNull=>id;public Guid? UserIdOrNull=>null;}
