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
const string billingRules="Prepaid: the shipper is the Bill To. Collect: the consignee is the Bill To.";
// Exercise the actual packet worker with an authenticated-service failure, bypassing AI indexing.
var packetType=typeof(OrderDocumentConversion).Assembly.GetType("Aurora.Api.Features.Routing.PacketMapping")!;
var packetEntry=Activator.CreateInstance(packetType,1,1,"TEST-BOL","Test payer")!;
packetType.GetProperty("Issuer")!.SetValue(packetEntry,"Test issuer");
var packetEntries=Array.CreateInstance(packetType,1);
packetEntries.SetValue(packetEntry,0);
var packetRead=typeof(OrderDocumentConversion).Assembly.GetType("Aurora.Api.Features.Routing.PdfPacketExtraction")!.GetMethod("Read")!;
var pdfBuilder=new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
pdfBuilder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
using(var pdfStream=new MemoryStream(pdfBuilder.Build()))
foreach(var status in new[]{System.Net.HttpStatusCode.Unauthorized,System.Net.HttpStatusCode.Forbidden})
{
    var calls=0;
    Func<Microsoft.AspNetCore.Http.IFormFile,string,bool,Task<JsonElement>> reject=(_,_,_)=>
    {
        calls++;
        throw new HttpRequestException("Rejected service credential",null,status);
    };
    var file=new Microsoft.AspNetCore.Http.FormFile(pdfStream,0,pdfStream.Length,"file","test.pdf");
    try
    {
        await (Task<JsonElement>)packetRead.Invoke(null,[file,"",Array.Empty<DocumentFormRule>(),reject,null,CancellationToken.None,packetEntries,null])!;
        throw new Exception("Service authentication failure must propagate");
    }
    catch(HttpRequestException ex)
    {
        Check(ex.StatusCode==status && calls==1,$"PDF {status} propagates without retry or content-error wrapping");
    }
    pdfStream.Position=0;
}
foreach (var form in new[] { "Lykes", "Other" })
{
    var imported = new OrderDocumentDraft { DocumentType=form, FreightTerms="Collect" };
    var headerFields = new Dictionary<string,object?>();
    Aurora.Integrations.Nova.FirebirdRoutingSource.ApplyImportHeader(headerFields, imported, "sample", false);
    Check(!headerFields.ContainsKey("PBPAYTYP"), "Other tenants retain their import pay type");
    Aurora.Integrations.Nova.FirebirdRoutingSource.ApplyImportHeader(headerFields, imported, "sample", true);
    Check(Equals(headerFields["PBPAYTYP"],"B"), $"Lykes tenant imports {form} form as 3rd Party");
}
foreach(var terms in new[]{"Prepaid","Collect","Third Party"})
{
 var billing=new OrderDocumentDraft{ FreightTerms=terms, ShipperAccountId="S1",ConsigneeAccountId="C2",Order=new(){Shipper="Sender",Customer="Receiver",OriginCity="Seattle",City="Austin"} };
 DocumentMappings.PrepareBilling(billing,billingRules);
 Check(terms switch {
     "Prepaid"=>billing.Order.BillTo=="Sender" && billing.Order.BillToCity=="Seattle" && billing.BillToAccountId=="S1",
     "Collect"=>billing.Order.BillTo=="Receiver" && billing.Order.BillToCity=="Austin" && billing.BillToAccountId=="C2",
     _=>billing.MappingQuestions.Single().Field=="BillTo" && billing.Order.BillTo==""}, "BOL freight terms "+terms);
}
var lykesBill=OrderDocumentImport.Parse("""[{"DocumentType":"Lykes","PrintedBillToAccountLine":"Account: UPS9001 - UPS SUPPLY CHAIN / MCO - MCO","PrintedLykesTag":"MCO3735721","HouseAirWaybill":"MCO3735721","TerminalCode":"MCO","LogoCompany":{"Name":"Lykes Cartage Company","Page":6},"Order":{}}]""").Single();
DocumentMappings.PrepareBilling(lykesBill,billingRules);
Check(lykesBill.BillToAccountId=="UPS9001" && lykesBill.Order.BillTo=="UPS SUPPLY CHAIN / MCO - MCO" &&
    lykesBill.Order.Id=="3735721" && lykesBill.Order.TerminalCode=="MCO" && lykesBill.BillOfLading=="MCO3735721" && lykesBill.LogoCompany is null,
    "Lykes Account, Tag, independent HAWB and Org survive logo billing");
var missingTag=OrderDocumentImport.Parse("""[{"DocumentType":"Lykes","HouseAirWaybill":"MCO3735721","TerminalCode":"DFW","Order":{}}]""").Single();
Check(missingTag.Order.Id=="" && missingTag.Order.TerminalCode=="DFW","Missing Lykes Tag stays blank and Org remains independent");
var explicitPayer=new OrderDocumentDraft{LogoCompany=new("Allstates WorldCargo",3),FreightTerms="Third Party",PrintedThirdPartyPayer="McKesson SDF",Order=new(){BillTo="MCKESSON SDF"}};
DocumentMappings.PrepareBilling(explicitPayer,billingRules);
Check(explicitPayer.Order.BillTo=="MCKESSON SDF" && explicitPayer.LogoCompany is null,"Marked third-party payer survives issuer logo");
var logoDraft=new OrderDocumentDraft{LogoCompany=new("Pegasus Logistics Group",1,"306 Airline Drive","Suite 100","Coppell","TX","75019","US",Phone:"469-671-0300"),FreightTerms="Third Party",BillToAccountId="OTHER",Order=new(){BillTo="Other payer",BillToCity="Spring"},MappingQuestions=[new(){Field="BOL"},new(){Field="BillTo"}]};
DocumentMappings.PrepareBilling(logoDraft,billingRules);
Check(logoDraft.Order.BillTo=="Pegasus Logistics Group" && logoDraft.Order.BillToCity=="Coppell" && logoDraft.BillToPhone=="469-671-0300" && logoDraft.BillToAccountId=="" && logoDraft.MappingQuestions.Single().Field=="BOL","Logo contact block overrides another payer and preserves unrelated questions");
NovaImportAddress.Apply(logoDraft,[new(){Name="Pegasus Logistics Group",Code="PEG",City="Coppell",State="TX",Zip="75019",Address="Alternate street"}]);
Check(logoDraft.Order.BillToAddress=="306 Airline Drive" && logoDraft.BillToAccountId=="PEG","Matched account preserves the logo's printed address");
foreach(var name in new string?[]{"ULINE, Inc.","U-Line Shipping Supplies",null}){
 var fallback=new OrderDocumentDraft{LogoCompany=name is null?null:new(name,1,Phone:"555-1234"),FreightTerms="Collect",Order=new(){Customer="Receiving company"}};
 DocumentMappings.PrepareBilling(fallback,billingRules);
 Check(fallback.Order.BillTo=="Receiving company" && fallback.BillToPhone is null && fallback.LogoCompany is null,"Uline and absent logo follow existing terms");
}
var reviewedAddress=new OrderDocumentDraft{EditedAddressParts=["delivery"],ConsigneeAccountId="C2",Order=new(){Customer="Receiver",Address="Edited dock"}};
NovaImportAddress.Apply(reviewedAddress,[new(){Code="C2",Name="Receiver",Address="Main dock"}]);
Check(reviewedAddress.Order.Address=="Edited dock","late address match preserves the reviewed address");
// Parse the reviewed Article layout through the same parser used by the PDF reader.
var articleReader=typeof(AtlasDeliveryTickets).GetMethod("ReadTicket",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
const string articleText="""
Delivery Ticket
Sample Recipient
123 Sample Lane
32308
Tallahassee
\uF095 5551234567
\uF0E0 sample@example.com
Order #: 3860024
Carrier: Waylanders Delivery
ORDER ID
Box ID
Description
Dimensions
Qty
#3860024
ART2107
Nera 5-Drawer Chest - Walnut
41x22x50 @ 202.83lbs
1
Delivery Service
Delivery Instruction
Ground-level Door
Dolly to door Deliver no earlier November 23rd
""";
var articleInput=articleText.Replace("\\uF095","\uF095").Replace("\\uF0E0","\uF0E0").ReplaceLineEndings("\n");
var article=(OrderDocumentDraft)articleReader.Invoke(null,[articleInput])!;
Check(article is not null && article.BillOfLading=="3860024" && article.Order.Reference=="3860024" && string.IsNullOrEmpty(article.Order.Id) && article.TagNumber=="","Article Order # maps to BOL and leaves PRO for Nova");
Check(article!.MappingSender=="Article" && article.MappingForm=="Delivery Ticket" && article.ConsigneeAccountId=="" && article.Order.Customer=="Sample Recipient","Article uses the recipient as uncoded consignee");
Check(article.Order.Weight==202.83m && article.Order.Pieces==1 && article.FreightLines.Single().Description=="ART2107 Nera 5-Drawer Chest - Walnut" && article.FreightLines[0].DimensionUnit=="","Article preserves freight and decimal pounds without inventing dimension units");
Check(article.Order.Notes.Contains("Delivery Service: Ground-level Door") && article.Order.Notes.Contains("November 23rd") && article.Order.Notes.Contains("sample@example.com") && article.LocalScheduledAt is null,"Article preserves service, handling and contact text without inventing an appointment");
Check(articleReader.Invoke(null,[articleInput.Replace("Ground-level Door","Unrecognized Service")]) is null,"Unrecognized Article layout falls back to configured extraction");
if(Environment.GetEnvironmentVariable("ATLAS_REPORT_CHECK") is {} atlasPath){
 using var file=File.OpenRead(atlasPath);
 var atlas=AtlasDeliveryTickets.Read(file,default);
 Check(atlas is {Count:6} && atlas.Sum(d=>d.FreightLines.Length)==18 && atlas.Single(d=>d.BillOfLading=="3833862").FreightLines.Length==9,"Atlas ticket continuation and all freight rows");
 Check(atlas![0].Order.Notes.Contains("November 23rd") && atlas[0].LocalScheduledAt is null,"Atlas lower bound stays in handling instructions");
}
var id=Guid.NewGuid();
foreach(var (accountCode,name) in new[]{("EXP1034","EXPEDITORS INTL - AUS TRANSCON"),("PEG9001","PEGASUS LOGISTICS - MCO")}){
 var account=new OrderDocumentDraft{PrintedBillToAccountLine=$"Account: {accountCode} - {name}"};account.ApplyPrintedAccount();
 Check(account.BillToAccountId==accountCode && account.Order.BillTo==name && account.ShipperAccountId=="","Account header ID and full name");
}
var twoLines=new OrderDocumentDraft{FreightLines=[new(){Pieces=1,CubeFt3=3},new(){Pieces=2,Length=12,Width=12,Height=12,DimensionUnit="in"}]};
twoLines.ApplyFreightTotals();
Check(twoLines.FreightLines[1].Volume==2 && twoLines.Order.Cube==5,"Per-line printed and calculated cube");
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
var tagged=OrderDocumentImport.Parse("""[{"documentType":"Lykes","printedLykesTag":"TPA3735950","tagNumber":"3739303","order":{}}]""",eastern,true).Single();
Check(tagged.Order.Id=="3735950" && tagged.Order.TerminalCode=="TPA","Only the printed Crown Tag supplies the PRO");
var untagged=OrderDocumentImport.Parse("""[{"tagNumber":"USXI-353471","order":{"id":"23683991"}},{"documentType":"AIT","terminalCode":"TPA/X","order":{}}]""",eastern,true);
Check(untagged.All(d=>d.TagNumber=="" && d.Order.Id=="") && untagged[0].Order.Notes.Contains("USXI-353471") && untagged[1].Order.TerminalCode=="TPA","Missing printed Tags go to review without normalizing carrier identifiers");
const string combinedPayer="COMPUCOM C/O AFS, P O BOX 18170, SHREVEPORT, LA 71138, US";
var combinedDraft=new OrderDocumentDraft{Order=new(){BillTo=combinedPayer,Shipper="Widgets, Inc., 12 East St, Suite 3, Portland, OR 97201",Customer="Receiver",Address="200 Elm St, Dallas, TX 75201"}};
combinedDraft.SeparatePartyAddresses();
Check(combinedDraft.Order.BillTo=="COMPUCOM C/O AFS" && combinedDraft.Order.BillToAddress=="P O BOX 18170" && combinedDraft.Order.BillToCity=="SHREVEPORT" && combinedDraft.Order.BillToState=="LA" && combinedDraft.Order.BillToPostalCode=="71138" && combinedDraft.Order.BillToCountry=="US","Split the printed third-party payer and its full mailing address");
Check(combinedDraft.Order.Shipper=="Widgets, Inc." && combinedDraft.Order.OriginAddress2=="Suite 3" && combinedDraft.Order.Address=="200 Elm St" && combinedDraft.Order.City=="Dallas","Split party blocks across shipper and consignee without splitting company suffixes");
var payerQuestion=new DocumentMappingQuestion{Field="BillTo"};
DocumentMappings.Answer(combinedDraft,payerQuestion,new MappingCandidate("Bill To",combinedPayer,3));
Check(combinedDraft.Order.BillTo=="COMPUCOM C/O AFS" && combinedDraft.Order.BillToPostalCode=="71138","A reviewer-selected payer populates the mailing fields");
var conflictDraft=new OrderDocumentDraft{Order=new(){BillTo=combinedPayer,BillToCity="BOSSIER CITY"}};
conflictDraft.SeparatePartyAddresses();
Check(conflictDraft.Order.BillTo==combinedPayer && conflictDraft.Order.BillToCity=="BOSSIER CITY","Preserve conflicting printed address data for review");
var ambiguousBol=OrderDocumentImport.Parse("""[{"mappingQuestions":[{"Field":"BOL","Candidates":[{"Label":"Delivery Receipt","Value":"ABC-42","Page":2}]}],"order":{}}]""").Single();
var bolQuestion=ambiguousBol.MappingQuestions.Single();
Check(bolQuestion.Field=="BOL" && bolQuestion.Candidates.Single().Page==2,"Ask for the BOL with its printed candidate and page");
DocumentMappings.Answer(ambiguousBol,bolQuestion,bolQuestion.Candidates.Single());
ambiguousBol.Normalize(allowBlankPro:true);
Check(ambiguousBol.Order.Id=="" && ambiguousBol.Order.Reference=="ABC-42" && ambiguousBol.MappingQuestions.Length==0,"Chosen BOL does not become a PRO");
var bolChoices=OrderDocumentImport.Parse("""[{"MappingQuestions":[{"Field":"BOL","Candidates":[]}],"PrintedIdentifiers":[{"Label":"Waybill #","Value":"DFW 3048520","Page":1},{"Label":"Ref #","Value":"0189868236","Page":1}],"ShipmentReference":"0189868236","order":{}}]""").Single();
DocumentMappings.RequireBol(bolChoices);
Check(bolChoices.MappingQuestions.Single().Candidates is [{Value:"DFW 3048520",Page:1},{Value:"0189868236",Page:1}] && bolChoices.BillOfLading=="","Missing BOL offers this waybill and reference once without picking either");
var structuredChoices=OrderDocumentImport.Parse("""[{"houseAirWaybill":"ABC-42","purchaseOrder":"00007","order":{}}]""").Single();
Check(structuredChoices.BillOfLading=="ABC-42" && structuredChoices.MappingQuestions.Length==0,"HAWB resolves BOL even with a purchase order present");
foreach(var label in new[]{"HAWB","BOL #","BOL No.","Bill of Lading Num","bill of lading number"})
{
    var labeled=new OrderDocumentDraft{PrintedIdentifiers=[new(label,"001-42",1)]};
    DocumentMappings.RequireBol(labeled);
    Check(labeled.Order.Reference=="001-42" && labeled.MappingQuestions.Length==0,"Resolve printed "+label);
}
var competing=new OrderDocumentDraft{PrintedIdentifiers=[new("BOL","123"),new("HAWB","456")]};
DocumentMappings.RequireBol(competing);
Check(competing.MappingQuestions.Single().Candidates.Length==2 && competing.BillOfLading=="","Competing explicit identifiers require review");
var missingIds=OrderDocumentImport.Parse("""[{"order":{}}]""").Single();
Check(missingIds.MappingQuestions.Single().Field=="BOL","An absent BOL requires review; a missing PRO does not");
Check(OrderDocumentImport.Parse("""[{"billOfLading":"42A","order":{}}]""").Single().MappingQuestions.Length==0,"Explicit BOL works with a blank PRO");
var input=JsonSerializer.Serialize(new[]{new OrderDocumentDraft {ImportId=id,NativeStatusId=999,TagNumber="MCO3706592",ShipmentReference="SHIP-42",HouseAirWaybill="HAWB-42",MasterAirWaybill="MAWB-42",Order=new(){Id="wrong",Reference="invented-bol",Customer="Consignee",Address="123 Main",Pieces=4,Weight=500,SourceId=id,TerminalId=id,ManifestId=id,CustomerId=id,NativeOrderId=42,Status="Dispatched",Revision=12}}});
foreach(var json in new[]{input,JsonSerializer.Serialize(input),JsonSerializer.Serialize("```json\n"+input+"\n```")})
{
 var draft=OrderDocumentImport.Parse(json).Single();var order=draft.Order;
 Check(draft.ImportId is null && draft.NativeStatusId is null,"Extraction must not assign import history or status identity");
 Check(order.Id=="3706592" && order.TerminalCode=="MCO" && order.Weight==500 && order.Pieces==4 && order.Address=="123 Main","Business data lost");
 Check(order.Reference=="HAWB-42","Use the labeled HAWB for the order reference");
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
 catch(DuplicateProException){Check(true,"Duplicate PRO is distinguishable from other creation failures");}
 Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_order")==3,"Duplicate save changed data");
}
var nativeTenant=new Context(Guid.NewGuid());
Check(NovaDocumentImport.PostalCode("32801-1234")=="328011234" && NovaDocumentImport.PostalCode("78754 4716")=="787544716" && NovaDocumentImport.PostalCode("K1A 0B1")=="K1A 0B1","ZIP+4 retains all digits within Nova nine-character field; other postal formats are preserved");
var nativeSource=new Aurora.Integrations.Nova.FirebirdRoutingSource(null!,nativeTenant,Microsoft.Extensions.Options.Options.Create(new Aurora.Integrations.Nova.FirebirdRoutingOptions{Enabled=true,TenantId=nativeTenant.TenantIdOrNull!.Value,SourceKey="test",ConnectionString="test"}));
var lykesHeader=NovaDocumentImport.Build(lykesBill,nativeSource.NewRecord("orders"),110,TimeZoneInfo.Utc).Values["header"];
Check(lykesHeader["PB_BILL_ID"]=="UPS9001" && lykesHeader["PBNUM"]=="3735721" && lykesHeader["PB_LOCATION"]=="MCO","Lykes native import carries the printed account code, numeric PRO and selected terminal");
foreach(var alias in new[]{"UNITED STATES","United States of America","usa"," U.S.A. "})
{
    var countryDraft=new OrderDocumentDraft{Order=new(){BillToCountry=alias,Country=alias,OriginCountry=alias}};
    var record=NovaDocumentImport.Build(countryDraft,nativeSource.NewRecord("orders"),110,TimeZoneInfo.Utc);
    Check(record.Values["header"]["PBBCOUNTRY"]=="US" && record.Values["pickup"]["OPS_COUNTRY"]=="US" && record.Values["delivery"]["OPS_COUNTRY"]=="US","Normalize all imported country fields: "+alias);
}
var canadianDraft=new OrderDocumentDraft{Order=new(){OriginCity="Vaughan",OriginState="Ontario",OriginCountry="Canada",BillToState="Ontario",BillToCountry="CANADA",State="Florida",Country="USA"}};
var canadianRecord=NovaDocumentImport.Build(canadianDraft,nativeSource.NewRecord("orders"),110,TimeZoneInfo.Utc);
Check(canadianRecord.Values["header"]["PBBSTATE"]=="ON" && canadianRecord.Values["header"]["PBBCOUNTRY"]=="CA" &&
    canadianRecord.Values["pickup"]["OPS_STATE"]=="ON" && canadianRecord.Values["pickup"]["OPS_COUNTRY"]=="CA" &&
    canadianRecord.Values["delivery"]["OPS_STATE"]=="FL" && canadianRecord.Values["delivery"]["OPS_COUNTRY"]=="US","Ontario to Florida import fits Nova address fields");
var combinedCanadian=new OrderDocumentDraft{Order=new(){BillTo="Example, 370 New Huntington Road, Vaughan, Ontario L4H 0R4, Canada"}};
combinedCanadian.SeparatePartyAddresses();
Check(combinedCanadian.Order.BillToCity=="Vaughan" && combinedCanadian.Order.BillToPostalCode=="L4H 0R4" &&
    combinedCanadian.Order.BillToState=="ON" && combinedCanadian.Order.BillToCountry=="CA","Split Canadian address with a full province name");
var reviewedPacketDraft = new OrderDocumentDraft { UseReviewedPacketBol=true, BillOfLading="05760575001", HouseAirWaybill="8NP-0159255-3", Order=new(){TerminalCode="MCO"} };
var reviewedHeader = NovaDocumentImport.Build(reviewedPacketDraft,nativeSource.NewRecord("orders"),110,eastern).Values["header"];
Check(reviewedHeader["PBBOL"]=="05760575001" && reviewedPacketDraft.HouseAirWaybill=="8NP-0159255-3", "Reviewed packet BOL survives Nova creation when the HAWB differs");
var nativeDraft=new OrderDocumentDraft{TagNumber="MCO003706592",ShipmentReference="SHIP",HouseAirWaybill="HAWB",MasterAirWaybill="MAWB",Order=new(){TerminalCode="MCO",Customer="Receiver",Shipper="Sender",Pieces=12,Weight=125,Cube=2,Notes="Freight: shelving",ScheduledAt=DateTimeOffset.Parse("2026-10-15T11:00:00-04:00")}};
var native=NovaDocumentImport.Build(nativeDraft,nativeSource.NewRecord("orders"),110,TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));
Check(native.Values["header"]["PBNUM"]=="3706592","Native import strips terminal prefix and leading zeroes");
Check(native.Values["header"]["PBBOL"]=="HAWB" && native.Values["header"]["PB_CUST_ORD"]=="MAWB" && native.Values["delivery"]["OPS_BOL"]=="","Native air waybill header fields remain distinct from the stop BOL");
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
reopened.Order.BillToState="Québec";
reopened.Order.BillToCountry="Canada";
var update=ImportedOrderMapping.Edit(savedRecord,reopened,TimeZoneInfo.Utc,storedNotes);
Check(update["header"]["PBBSTATE"]=="QC" && update["header"]["PBBCOUNTRY"]=="CA","Edited import normalizes a spelled-out province and country");
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
var addressDraft=new OrderDocumentDraft{Order=new(){Customer=" existing consignee ",City="ORLANDO",State="Florida",PostalCode="32801",Address="Extracted street",BillTo="Unmatched payer"}};
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
 var p=OrderDocumentImport.Parse(File.ReadAllText(Path.Combine(extractionDir,"LYKES0003714580B_2.TIF.updated.json")),eastern,true);
 Check(p.Single().Order.Id=="" && p[0].Order.TerminalCode=="DFW" && p[0].Order.Cube==55.56m,"Actual Pegasus TIF extraction yields approved PRO, terminal and cube");
 Check(p[0].Order.Pieces==1 && p[0].Order.Weight==453.2m && TimeZoneInfo.ConvertTime(p[0].Order.ScheduledAt,eastern).DateTime==new DateTime(2026,11,2,7,0,0),"Actual Pegasus freight and delivery start match bill");
 var n=OrderDocumentImport.Parse(File.ReadAllText(Path.Combine(extractionDir,"noproLYKES0003733336B_1.TIF.updated.json")),eastern,true);
 Check(n[0].Order.Id=="" && n[0].Order.Notes.Contains("USXI-353471"),"Actual Expeditors scan leaves PRO blank and retains carrier identifier");
 Check(n[0].Order.Pieces==28 && n[0].Order.Notes.Contains("page 2 not supplied"),"Actual partial scan sums supplied line quantities and flags missing page");
 p[0].TagNumber="DFW42"; // Reviewer supplies the missing Tag.
 var mapped=NovaDocumentImport.Build(p[0],nativeSource.NewRecord("orders"),110,eastern);
 Check(mapped.Values["header"]["PB_LOCATION"]=="DFW" && mapped.Values["header"]["PB_TTL_CUBE"]=="55.56" && mapped.Values["delivery"]["OPS_APPT_LOW"]=="2026-11-02T07:00:00" && mapped.Values["delivery"]["OPS_APPT_HIGH"]=="2026-11-02T18:00:00","Corrected extraction survives native save mapping");
 Console.WriteLine($"{count} checks including actual Danielle TIFF extractions passed.");
}
if(Environment.GetEnvironmentVariable("CROWN_REPORT_CHECK") is {} crownPath)
{
 using var crownFile=File.OpenRead(crownPath);
 var crown=CrownOrderReport.Read(crownFile,TimeZoneInfo.FindSystemTimeZoneById("America/New_York"))!;
 Check(crown.Count==1797 && crown.Select(d=>d.TagNumber).Distinct().Count()==1797,"Crown report groups all 2128 rows into 1797 unique full Tags");
 Check(crown.GroupBy(d=>d.Order.Id).Where(g=>g.Count()>1).Sum(g=>g.Count())==12 && crown.All(d=>OrderDocumentDraft.SplitTag(d.TagNumber).Pro==d.Order.Id),"All Crown PROs follow the printed Tags, including the 12 collisions");
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
if (Environment.GetEnvironmentVariable("LYKES_DAILY_REPORT_CHECK") is {} dailyPath)
{
    using var workbook = File.OpenRead(dailyPath);
    Check(CrownOrderReport.Read(workbook,eastern) is null,"Daily workbook is distinct from Crown report");
    workbook.Position = 0;
    var shipments = LykesDailyReport.Read(workbook,eastern)!;
    Check(shipments.Count == 67 && shipments.Select(d=>d.Order.Id).Distinct().Count()==67,"Daily report contains 67 unique PROs");
    Check(shipments.All(d=>d.FreightLines.Length==1 && d.MappingQuestions.Length==0 && d.Order.AppointmentFrom is null && d.Order.AppointmentTo is null),"One freight line per shipment without mapping questions or imported appointment");
    Check(shipments.Sum(d=>d.Order.Pieces)==224 && shipments.Sum(d=>d.Order.Weight)==36030 && shipments.Sum(d=>d.FreightLines[0].DimWeightLb)==4262,"All daily report freight quantities reconcile");
    var first = shipments.First();
    var header = NovaDocumentImport.Build(first,nativeSource.NewRecord("orders"),110,eastern).Values["header"];
    Check(header["PBNUM"]=="3739065" && header["PBBOL"]=="0038067363" && first.ShipmentReference=="0038067363" && first.BillToAccountId=="" && header["PBBNAME"]=="AVERITT EXPRESS NEXT DAY" && header["PB_LOCATION"]=="MCO","First daily report shipment uses the bill-to name and preserves leading zeroes");
    Check(shipments.All(d=>NovaDocumentImport.Build(d,nativeSource.NewRecord("orders"),110,eastern).Values["header"]["PB_CUST_ORD"]==d.MasterAirWaybill),"Daily report MAWB uses the Lykes customer order field");
    Console.WriteLine($"{count} total checks including the daily report passed.");
}
var billToIndex = new Aurora.Integrations.Nova.DailyBillToIndex([
    ("NON","Nonstop Delivery, Inc."), ("A","Acme Inc"), ("B","Acme L.L.C."),
    ("NEXT","Averitt Express Next Day"), ("SAME","Averitt Express Same Day"), ("INC","Inc Research")
]);
Check(billToIndex.TryFind("Nonstop-Delivery Corporation", out var normalizedCode) && normalizedCode=="NON","Spaces, punctuation and trailing legal endings normalize on both sides");
Check(billToIndex.TryFind("Acme Corp.", out var ambiguousCode) && ambiguousCode is null,"Ambiguous normalized accounts require review");
Check(!billToIndex.TryFind("Research",out _) && !billToIndex.TryFind("Averitt Express",out _) &&
      billToIndex.TryFind("Averitt Express Same-Day",out var sameDay) && sameDay=="SAME","Interior words and Averitt service levels stay distinct");
if (Environment.GetEnvironmentVariable("LYKES_NOV11_REPORT_CHECK") is {} novemberPath)
{
    using var workbook = File.OpenRead(novemberPath);
    var shipments = LykesDailyReport.Read(workbook, eastern)!;
    Check(shipments.Count == 73 && shipments.All(d => d.BillToAccountId == ""), "Ignore the supplied account IDs on every November 11 shipment");
    Check(shipments.Count(d => d.Order.BillTo == "NONSTOPDELIVERY, INC.") == 28, "Split a customer name even without spaces around the dash");
    Check(shipments.Count(d => d.Order.BillTo == "AVERITT EXPRESS NEXT DAY") == 17 &&
          shipments.Count(d => d.Order.BillTo == "AVERITT EXPRESS SAME DAY") == 2, "Retain both Averitt services");
    var accounts = new[] {
        new NovaImportAddress { Code="NEXT", Name="AVERITT EXPRESS NEXT DAY", Address="1 Example Rd" },
        new NovaImportAddress { Code="SAME", Name="AVERITT EXPRESS SAME DAY", Address="2 Example Rd" }
    };
    foreach (var account in accounts)
    {
        var draft = shipments.First(d => d.Order.BillTo == account.Name);
        NovaImportAddress.Apply(draft, accounts);
        var header = NovaDocumentImport.Build(draft, nativeSource.NewRecord("orders"), 110, eastern).Values["header"];
        Check(header["PB_BILL_ID"] == account.Code, "Write the account ID selected by the Averitt service name");
    }
    Console.WriteLine($"{count} checks including the November 11 report passed.");
}
sealed class Context(Guid id):ITenantContext{public Guid? TenantIdOrNull=>id;public Guid? UserIdOrNull=>null;}
