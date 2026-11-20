using Aurora.Api.Features.Auth;
using Aurora.Api.Features.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Integrations.Nova;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

var config=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(File.ReadAllText(Environment.GetEnvironmentVariable("FIREBIRD_CHECK_CONFIG")!))!;
var cs=new NpgsqlConnectionStringBuilder(config["ConnectionStrings:Aurora"].GetString());
if(cs.Host!="127.0.0.1" || cs.Port!=55432 || cs.Database!="aurora_test")throw new Exception("Local test database required.");
var credentials=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText("deploy/private/local-test/credentials.json"))!;
var ownerCs=new NpgsqlConnectionStringBuilder(cs.ConnectionString){Username="postgres",Password=credentials["postgres"]};
await using var owner=new NpgsqlConnection(ownerCs.ConnectionString);await owner.OpenAsync();
var admin=await owner.QuerySingleAsync<Guid>("""SELECT "Id" FROM "AspNetUsers" WHERE "UserName"='bbatts'""");
var a=Guid.NewGuid();var b=Guid.NewGuid();
await using var ds=NpgsqlDataSource.Create(cs.ConnectionString);
var context=new Context(a,admin);
var factory=new NpgsqlConnectionFactory(ds,context);
var services=new ServiceCollection();services.AddOptions();services.Configure<FirebirdRoutingOptions>(_=>{});
using var provider=services.BuildServiceProvider();
var protection=new EphemeralDataProtectionProvider();
var nova=new NovaUserConnections(protection,provider.GetRequiredService<IOptionsMonitor<FirebirdRoutingOptions>>());
var service=new TenantSettingsService(factory,context,nova,protection);
var history=new OrderImportHistoryStore(factory,context,NullLogger<OrderImportHistoryStore>.Instance);
int count=0;
void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
async Task Reject(Func<Task> action,string label){try{await action();}catch(FormatException){Check(true,label);return;}throw new Exception(label);}
try
{
 foreach(var id in new[]{a,b})await owner.ExecuteAsync("""
 INSERT INTO tenant(id,name,slug,is_active,created_utc) VALUES(@id,'Settings check',@slug,true,now());
 INSERT INTO user_tenant(tenant_id,user_id,is_default) VALUES(@id,@admin,false);
 INSERT INTO tenant_product(tenant_id,product_code,instance_url,is_active) VALUES(@id,'freightops','https://old.example.invalid/',true);
 INSERT INTO "AspNetUserRoles"("TenantId","UserId","RoleId") SELECT @id,@admin,"Id" FROM "AspNetRoles" WHERE "Name"='aurora:Admin';
 """,new{id,admin,slug="settings-check-"+id.ToString("N")});
 var input=await service.Read(default);
 var globalRules=input.GlobalDocumentSettings!.Instructions;
 Check(input.DocumentExtractionInstructions=="","a new company starts with shared extraction rules");
 const string companyInstructions="Read a parcel label as the shipment reference.";
 input.DocumentExtractionInstructions=companyInstructions;
 input.DocumentFormRules=[new(){Issuer="Lykes Cartage Company",Form="Lykes Freight Bill",Account="UPS9001",Instructions="Use the customer Account header."}];
 Check(input.DriverCostPerHour==0,"new tenant defaults to zero driver cost");
 Check(input.Navigation is {Optimization:true,DataEntry:true,FreightOps:true,Hub:true,Nova:true},"new tenant shows all menu options by default");
 input.CompanyName=" Updated company ";
 input.Navigation=new(){Optimization=false,DataEntry=true,FreightOps=false,Hub=false,Nova=false};
 input.DriverCostPerHour=27.5m;
 input.Nova=new(){Host="192.0.2.1",Database="test.fdb",UserName="test",Password="nova-test-secret"};
 input.FreightOps=new(){ApiUrl="https://freight.example.invalid/",UserName="test",Password="freight-test-secret"};
 input.DispatchTrack=new(){ApiUrl="https://dispatch.example.invalid/orders",ApiCode="dispatch-code",ApiKey="dispatch-key"};
 input.Zenventory=new(){BaseUrl="https://zen.example.invalid/",ApiKey="zen-key",ApiSecret="zen-secret",LegacyBaseUrl="https://legacy.example.invalid/",SecureKey="zen-secure-key"};
 await service.Save(input,default);
 Check(await owner.ExecuteScalarAsync<string>("SELECT instance_url FROM tenant_product WHERE tenant_id=@a AND product_code='freightops'",new{a})==input.FreightOps.ApiUrl,"launcher uses saved FreightOps connection URL");
 Check(await owner.ExecuteScalarAsync<string>("SELECT instance_url FROM tenant_product WHERE tenant_id=@b AND product_code='freightops'",new{b})=="https://old.example.invalid/","saving launcher URL does not change another tenant");
 var saved=await service.Read(default);
 var scoped=saved.DocumentFormRules!.Single();
 Check(scoped.Matches(" LYKES Cartage Company ","Lykes Freight Bill","ups9001") && !scoped.Matches("Other carrier","Lykes Freight Bill","UPS9001") &&
     !scoped.Matches("Lykes Cartage Company","Lykes Freight Bill","OTHER"),"PDF settings match only this issuer, form and customer");
 Check(saved.DocumentExtractionInstructions==companyInstructions,"company extraction instructions round-trip");
 Check(saved.CompanyName=="Updated company","company name saved and trimmed");
 Check(saved.Navigation is {Optimization:false,DataEntry:true,FreightOps:false,Hub:false,Nova:false},"menu preferences persist independently");
 Check(!saved.Navigation!.IsVisible("aurora") && saved.Navigation.DataEntry,"Data Entry can remain visible with Optimization hidden");
 Check(await owner.ExecuteScalarAsync<string>("SELECT name FROM tenant WHERE id=@b",new{b})=="Settings check","company rename isolated to current tenant");
 Check(saved.DriverCostPerHour==27.5m,"global hourly driver cost persists");
 await using(var db=await factory.OpenConnectionAsync())Check(await db.ExecuteScalarAsync<decimal>("SELECT public.aurora_driver_cost_per_hour()") == 27.5m,"optimization reads saved hourly cost");
 Check(saved.Revision==1 && saved.Nova.HasPassword && saved.FreightOps.HasPassword && saved.DispatchTrack.HasApiCode && saved.DispatchTrack.HasApiKey && saved.Zenventory.HasApiSecret && saved.Zenventory.HasSecureKey,"all four connections saved with configured flags");
 Check(saved.Nova.Password is null && saved.FreightOps.Password is null && saved.DispatchTrack.ApiKey is null && saved.DispatchTrack.ApiCode is null && saved.Zenventory.ApiKey is null && saved.Zenventory.ApiSecret is null && saved.Zenventory.SecureKey is null,"read response excludes every secret");
 var stored=await owner.QuerySingleAsync<string>("SELECT row_to_json(s)::text FROM tenant_settings s WHERE tenant_id=@a",new{a});
 Check(!new[]{"nova-test-secret","freight-test-secret","dispatch-code","dispatch-key","zen-key","zen-secret","zen-secure-key"}.Any(stored.Contains),"all credentials encrypted at rest");
 await service.Save(saved,default);
 var again=await service.Read(default);Check(again.Revision==2 && again.Zenventory.HasApiKey,"blank fields retain saved secrets");
 await Reject(()=>service.Save(saved,default),"stale edit rejected");
 var badName=await service.Read(default);badName.CompanyName=" ";
 await Reject(()=>service.Save(badName,default),"blank company name rejected");
 badName.CompanyName=new string('x',201);await Reject(()=>service.Save(badName,default),"overlong company name rejected");
 var compatibility=await service.Read(default);compatibility.Navigation=null;compatibility.DocumentExtractionInstructions=null;compatibility.DocumentFormRules=null;
 await service.Save(compatibility,default);
 Check((await service.Read(default)).DocumentExtractionInstructions==companyInstructions,"older settings clients preserve company instructions");
 var tooLong=await service.Read(default);tooLong.DocumentExtractionInstructions=new string('a',TenantSettingsDto.MaxDocumentInstructionsLength+1);
 await Reject(()=>service.Save(tooLong,default),"overlong company instructions are rejected");
 Check((await service.Read(default)).Navigation!.Optimization==false,"settings clients omitting menu preferences preserve saved preferences");
 var invalid=await service.Read(default);invalid.DriverCostPerHour=-1;
 await Reject(()=>service.Save(invalid,default),"negative driver cost rejected");
 invalid.DriverCostPerHour=1.00001m;await Reject(()=>service.Save(invalid,default),"excess driver cost precision rejected");
 invalid.DriverCostPerHour=35.25m;await service.Save(invalid,default);
 Check((await service.Read(default)).DriverCostPerHour==35.25m,"administrator can change hourly driver cost");
 var moved=await service.Read(default);moved.FreightOps.ApiUrl="https://new.example.invalid/";moved.FreightOps.Password="replacement-secret";
 await service.Save(moved,default);
 Check(await owner.ExecuteScalarAsync<string>("SELECT instance_url FROM tenant_product WHERE tenant_id=@a AND product_code='freightops'",new{a})==moved.FreightOps.ApiUrl,"changing Connections also changes launcher destination");
 var urlOnly=await service.Read(default);
 urlOnly.FreightOps=new(){ApiUrl="https://launcher.example.invalid/"};
 await service.Save(urlOnly,default);
 var launcher=await service.Read(default);
 Check(launcher.FreightOps.ApiUrl==urlOnly.FreightOps.ApiUrl && launcher.FreightOps.UserName=="" && !launcher.FreightOps.HasPassword,"URL-only FreightOps connection round trips without retaining old credentials");
 Check(await owner.ExecuteScalarAsync<string>("SELECT instance_url FROM tenant_product WHERE tenant_id=@a AND product_code='freightops'",new{a})==urlOnly.FreightOps.ApiUrl,"URL-only connection updates the FreightOps launcher");
 var changed=await service.Read(default);changed.DispatchTrack.ApiUrl="https://other.example.invalid/";
 await Reject(()=>service.Save(changed,default),"changed destination requires fresh credentials");
 changed=await service.Read(default);changed.Nova.Host="192.0.2.2";changed.Nova.Password="new-secret";
 await owner.ExecuteAsync("UPDATE aurora_sync_connection SET enabled=true WHERE tenant_id=@a AND provider='nova'",new{a});
 await service.Save(changed,default);
 Check((await service.Read(default)).Nova.Host==changed.Nova.Host,"administrator can save a different Nova database");
 await using(var db=await factory.OpenConnectionAsync())
  Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM public.aurora_sync_credentials()")==0,"the original sync cannot use the replacement database");
 var beforeTest=await service.Read(default);
 var differentPath=new NovaConnectionSettings { Host=beforeTest.Nova.Host, Port=beforeTest.Nova.Port,
     Database="different.fdb", UserName=beforeTest.Nova.UserName };
 await Reject(()=>service.TestNovaConnection(differentPath,default),"testing a different path requires its own password");
 if(config.TryGetValue("FirebirdRouting:ConnectionString",out var fixture))
 {
  var fb=new FbConnectionStringBuilder(fixture.GetString());
  if(fb.DataSource=="172.23.0.5" && fb.Database.Equals(@"D:\db\wayl_test\operations.fdb",StringComparison.OrdinalIgnoreCase))
  {
   var probe=new NovaConnectionSettings {Host=fb.DataSource, Port=fb.Port, Database=fb.Database, UserName=fb.UserID, Password=fb.Password};
   await service.TestNovaConnection(probe,default);
   var afterTest=await service.Read(default);
   Check(afterTest.Revision==beforeTest.Revision && afterTest.Nova.Database==beforeTest.Nova.Database,
       "testing an alternate database succeeds without saving it to a tenant that has a sync source");
  }
 }
 context.Company=b;var other=await service.Read(default);
 Check(other.DocumentFormRules is {Count:0},"PDF customer rules isolated by tenant");
 Check(other.DocumentExtractionInstructions=="","second tenant does not receive the first tenant's instructions");
 other.DocumentExtractionInstructions="Temporary rule";await service.Save(other,default);
 other=await service.Read(default);other.DocumentExtractionInstructions="";await service.Save(other,default);
 Check((await service.Read(default)).DocumentExtractionInstructions=="","an administrator can explicitly clear extraction instructions");
 Check(other.DriverCostPerHour==0,"global rate isolated per company");
 Check(other.Navigation is {Optimization:true,DataEntry:true},"menu preferences isolated per tenant");
 Check(!other.Nova.HasPassword && !other.DispatchTrack.HasApiKey,"second tenant sees no first-tenant connections");
 await using(var db=await factory.OpenConnectionAsync())Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM tenant_settings WHERE tenant_id=@a",new{a})==0,"database row security hides another tenant");
 context.Company=a;
 var batch=await history.Start("C:\\fakepath\\shipping.pdf",default);
 var entry=(await history.List(default)).Single();
 Check(entry.FileName=="shipping.pdf" && entry.Status=="Converting","import history sanitizes filenames and records start");
 await Reject(()=>history.Validate(batch,default),"unfinished conversion cannot receive order credits");
 await history.Converted(batch,2,default);
 await history.Validate(batch,default);
 await history.RecordCreated(batch,"123");await history.RecordCreated(batch,"123");
 entry=(await history.List(default)).Single();
 Check(entry.CreatedCount==1 && entry.ExtractedCount==2 && entry.Status=="Orders created","history counts unique successful orders");
 Check(entry.CreatedPros.SequenceEqual(new[]{"123"}),"history returns distinct created PROs for order links");
 await history.RequireCreatedOrder(batch,"123",default);
 Check(true,"created order link resolves within tenant history");
 try{await history.RequireCreatedOrder(batch,"999",default);throw new Exception("Unrelated order link accepted");}catch(KeyNotFoundException){Check(true,"history link cannot address an unrelated PRO");}
 var failed=await history.Start("pasted-orders.txt",default);await history.Failed(failed,"Conversion timed out.");
 Check((await history.List(default)).First() is {Status:"Failed",FailureReason:"Conversion timed out."},"failed conversion retained in recent imports");
 var draft=new OrderDocumentDraft { ImportId=batch, ImportRowId=Guid.NewGuid(), ImportRowNumber=2, TagNumber="456" };
 await history.TrackCreation(draft,()=>Task.FromResult(Results.BadRequest(new ApiErrorDto("Bill-to state allows 2 characters.")) as IResult));
 await history.TrackCreation(draft,()=>Task.FromResult(Results.BadRequest(new ApiErrorDto("Bill-to state allows 2 characters.")) as IResult));
 var detail=(await history.Failures(batch,default)).Single();
 Check(detail.OrderLabel.Contains("456") && detail.Reason=="Bill-to state allows 2 characters.","order failure retains reason and identifier across repeated attempts");
 var localFailure=new ImportFailure(draft.ImportRowId,draft.FailureLabel(),"Invalid terminal prefix.");
 Check(await history.SaveReviewCounts(batch,new(0,1,[localFailure]),default),"review stores client-side validation reason with counts");
 Check((await history.Failures(batch,default)).Any(f=>f.Reason==localFailure.Reason),"local validation reason survives history reload");
 var legacy=await history.Start("legacy-duplicates.xlsx",default);
 await history.Converted(legacy,2,default,[new(Guid.NewGuid(),"Order 1","That PRO number already exists.")]);
 await history.SaveReviewCounts(legacy,new(1,0),default);
 Check(!(await history.List(default)).Single(h=>h.Id==legacy).HasOrderFailures && !(await history.Failures(legacy,default)).Any(),"skipped legacy duplicate has no failure details");
 await history.SaveReviewCounts(legacy,new(1,1,[new(Guid.NewGuid(),"Order 2","Invalid terminal prefix.")]),default);
 Check((await history.List(default)).Single(h=>h.Id==legacy).HasOrderFailures && (await history.Failures(legacy,default)).Single().Reason=="Invalid terminal prefix.","mixed batch retains its actual failure");
 context.Company=b;
 Check(!(await history.Failures(batch,default)).Any(),"order failure details isolated by tenant");
 Check(!(await history.List(default)).Any(),"history isolated by tenant");
 try{await history.RequireCreatedOrder(batch,"123",default);throw new Exception("Cross-tenant order link accepted");}catch(KeyNotFoundException){Check(true,"order link cannot cross tenant boundaries");}
 await Reject(()=>history.Validate(batch,default),"another tenant cannot attach to import");
 await history.RecordCreated(batch,"999");context.Company=a;
 Check((await history.List(default)).Single(x=>x.Id==batch).CreatedCount==1,"cross-tenant history update blocked");
 await owner.ExecuteAsync("DELETE FROM \"AspNetUserRoles\" WHERE \"TenantId\"=@a AND \"UserId\"=@admin",new{a,admin});
 await using(var db=await factory.OpenConnectionAsync())
 {
  Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM tenant_settings")==0,"ordinary planner cannot read connection settings");
  var menu=JsonSerializer.Deserialize<TenantNavigationSettings>((await db.ExecuteScalarAsync<string>("SELECT public.aurora_tenant_navigation()"))!);
  Check(menu is {Optimization:false,DataEntry:true},"ordinary member can read menu preferences without secrets");
  Check((await history.List(default)).Count()==3,"ordinary member can view company import history");
  Check(await db.ExecuteScalarAsync<decimal>("SELECT public.aurora_driver_cost_per_hour()")==35.25m,"ordinary planner can use only the global labor rate");
 }
 context.User=Guid.NewGuid();
 Check(!(await history.List(default)).Any(),"nonmember cannot read import history");
 await Reject(()=>history.Validate(batch,default),"another user cannot attach to import");
 await using(var db=await factory.OpenConnectionAsync())
 {
  Check(await db.ExecuteScalarAsync<decimal?>("SELECT public.aurora_driver_cost_per_hour()") is null,"nonmember cannot read driver rate");
  Check(await db.ExecuteScalarAsync<string?>("SELECT public.aurora_tenant_navigation()") is null,"nonmember cannot read menu preferences");
  Check(await db.ExecuteScalarAsync<string?>("SELECT public.aurora_document_extraction_instructions()") is null,"nonmember cannot read extraction instructions");
  Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM tenant_settings")==0,"non-admin cannot read settings");
  Check(await db.ExecuteAsync("UPDATE tenant_settings SET revision=revision+1")==0,"non-admin cannot change settings");
  Check(await db.ExecuteScalarAsync<string?>("SELECT public.aurora_tenant_nova_connection()") is null,"nonmember cannot resolve tenant Nova credentials");
 }
 context.User=admin;
 // Exercise both conversion paths with tenant-scoped PostgreSQL access and an in-memory Hub.
 var rules=new DocumentMappingStore(factory,context);
 var rule=new DocumentMappingRule("Fixture Sender","Fixture Ticket","PRO","Load ID");
 await rules.Save(rule,default);
 OrderDocumentDraft Ambiguous()=>new(){MappingSender=rule.Sender,MappingForm=rule.Form,MappingQuestions=[
     new(){Field="PRO",Candidates=[new("Order #","100",1),new("Load ID","200",2)]}]};
 var mapped=Ambiguous();
 await rules.Apply([mapped],false,default);
 Check(mapped.Order.Id=="200" && mapped.MappingQuestions.Length==0 && mapped.MappingDecisions.Single().Contains("page 2"),"remembered label resolves the current ticket with printed evidence");
 var strict=Ambiguous();await rules.Apply([strict],true,default);
 Check(strict.MappingQuestions.Length==1,"remembered field cannot bypass printed Lykes tag policy");
 context.Company=b;
 var isolated=Ambiguous();await rules.Apply([isolated],false,default);
 Check(isolated.MappingQuestions.Length==1,"remembered mapping does not cross tenant boundaries");
 context.Company=a;
 using var hub=new SampleHub();
 var hubSettings=new ConfigurationManager();
 foreach(var id in new[]{a,b})
 {
  hubSettings[$"DocumentConversion:Tenants:{id:D}:BaseUrl"]="https://hub.example.invalid";
  hubSettings[$"DocumentConversion:Tenants:{id:D}:ServiceCredential"]=id.ToString();
 }
 var options=Options.Create(new FirebirdRoutingOptions());
 var conversion=new OrderDocumentConversion(context,hubSettings,hub,factory,history,new FirebirdRoutingSource(factory,context,options),options);
 using var sampleStream=new MemoryStream("sample order"u8.ToArray());
 var sample=new FormFile(sampleStream,0,sampleStream.Length,"file","sample.txt");
 const string unsaved="Use the reference in the second column.";
 var preview=await conversion.Convert(sample,unsaved,default);
 Check(hub.Prompt.Contains(globalRules),"sample preview includes the shared global instructions");
 var blockedPreview=await conversion.Convert(sample,unsaved,default,"Unauthorized global draft");
 Check(blockedPreview is Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult,"ordinary member cannot override global instructions in previews");
 var samples=(preview as IValueHttpResult)?.Value as DocumentExtractionSample[];
 Check(samples is [{Pro:"42"}] && samples[0].Fields.GetProperty("TagNumber").GetString()=="MCO42" && hub.Prompt.EndsWith(unsaved) && !hub.Prompt.Contains(companyInstructions),"preview uses unsaved instructions and returns extracted fields");
 Check((await history.List(default)).Count()==3,"a sample preview leaves import history alone");
 var regular=await conversion.Convert(sample,null,default);
 var drafts=(regular as IValueHttpResult)?.Value as List<OrderDocumentDraft>;
 Check(drafts is [{ImportId:not null}] && hub.Prompt.EndsWith(companyInstructions) && !hub.Prompt.Contains(unsaved),"ordinary member conversion uses the saved instructions after a preview");
 Check(hub.Prompt.Contains("Return ONLY a JSON array") && hub.Credential==a.ToString(),"shared response contract and authenticated tenant credential reach the Hub");
 // A two-page PDF goes through the same classifier in admin preview and conversion.
 var packet=new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
 packet.AddPage(200,200);packet.AddPage(200,200);
 using var pdfStream=new MemoryStream(packet.Build());
 var pdfSample=new FormFile(pdfStream,0,pdfStream.Length,"file","two-issuers.pdf");
 hub.PacketFixture=true;
 var packetPreview=await conversion.Convert(pdfSample,"",default);
 var packetRows=(packetPreview as IValueHttpResult)?.Value as DocumentExtractionSample[];
 Check(packetRows is {Length:2} && packetRows[0].Pro=="3735721" &&
     packetRows[0].Fields.GetProperty("BillToAccountId").GetString()=="UPS9001" &&
     packetRows[0].Fields.GetProperty("SourcePages")[0].GetInt32()==1,
     "two-pass sample preview returns normalized Lykes billing and original page");
 Check(hub.ScopedPromptSeen && hub.OtherPromptIsolated,"customer instructions reach only the matching shipment");
 hubSettings[$"DocumentConversion:Tenants:{a:D}:RequirePrintedLykesTag"]="true";
 var proRules=new List<DocumentFormRule>{new(){Issuer="Unrelated issuer",UsePrintedPro=true,Instructions="Use Bill No. for BOL."}};
 async Task<string> SecondPro()
 {
     var sampleResult=await conversion.Convert(pdfSample,"",default,previewRules:proRules);
     return ((DocumentExtractionSample[])((IValueHttpResult)sampleResult).Value!)[1].Pro;
 }
 hub.SeparatePro=true;
 Check(await SecondPro()=="3456789","a scoped printed PRO survives the tenant Tag-only default");
 hub.SeparatePro=false;
 Check(await SecondPro()=="","the separate-PRO rule leaves PRO blank when only the bill number is present");
 hubSettings[$"DocumentConversion:Tenants:{a:D}:RequirePrintedLykesTag"]="false";
 var packetHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pdfStream.ToArray()));
 await owner.ExecuteAsync("INSERT INTO document_packet_mapping VALUES (@a,@packetHash,1,1,'TEST-001','Fixture payer'),(@a,@packetHash,2,2,'TEST-002','Fixture payer')",new{a,packetHash});
 var preselected=await conversion.Convert(pdfSample,"",default);
 var preselectedRows=(DocumentExtractionSample[])((IValueHttpResult)preselected).Value!;
 Check(preselectedRows[1].Fields.GetProperty("BillOfLading").GetString()=="TEST-002" &&
     preselectedRows.All(r=>r.Fields.GetProperty("MappingQuestions").GetArrayLength()==0),"exact packet applies provisional values without mapping questions");
 context.Company=b;
 var untouched=await conversion.Convert(pdfSample,"",default);
 Check(((DocumentExtractionSample[])((IValueHttpResult)untouched).Value!)[1].Fields.GetProperty("BillOfLading").GetString()!="TEST-002","same PDF in another tenant does not use packet selections");
 context.Company=a;
 hub.CopyFixture=true;
 var longPacket=new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
 for(int page=0;page<7;page++)longPacket.AddPage(200,200);
 using var longStream=new MemoryStream(longPacket.Build());
 var copies=await conversion.Convert(new FormFile(longStream,0,longStream.Length,"file","copies.pdf"),"",default);
 var copyRows=(copies as IValueHttpResult)?.Value as DocumentExtractionSample[];
 Check(copyRows is {Length:2} && copyRows[0].Fields.GetProperty("SourcePages").Deserialize<int[]>()!.SequenceEqual(new[]{5,6}) &&
     copyRows[1].Fields.GetProperty("SourcePages")[0].GetInt32()==7,"merge stamped and clean copies at an index boundary and keep distinct shipment");
 hub.CopyFixture=false;
 await owner.ExecuteAsync("UPDATE document_packet_mapping SET issuer='Unrelated issuer',form='Waybill',nova_assigns_pro=true WHERE tenant_id=@a AND file_sha256=@packetHash",new{a,packetHash});
 var indexCalls=hub.IndexCalls;
 var reviewed=await conversion.Convert(pdfSample,"",default);
 var reviewedRows=(DocumentExtractionSample[])((IValueHttpResult)reviewed).Value!;
 Check(hub.IndexCalls==indexCalls && reviewedRows.All(r=>r.Pro=="" && r.Fields.GetProperty("Order").GetProperty("BillTo").GetString()=="Fixture payer"),
     "visually indexed packets use the saved issuer and payer and let Nova assign the PRO");
 var firstReady=new TaskCompletionSource<OrderDocumentDraft>(TaskCreationOptions.RunContinuationsAsynchronously);
 hub.HoldSecond=new(TaskCreationOptions.RunContinuationsAsynchronously);
 var streamedConversion=conversion.Convert(pdfSample,null,default,deferAddressLookup:true,
     ordersReady:rows=>firstReady.TrySetResult(rows[0]));
 var early=await firstReady.Task.WaitAsync(TimeSpan.FromSeconds(10));
 Check(!streamedConversion.IsCompleted && early.ImportId.HasValue && early.ImportRowId!=Guid.Empty &&
     early.BillOfLading=="TEST-001" && early.MappingQuestions.Length==0,"first editable draft is available with final packet mappings while second shipment is blocked");
 hub.HoldSecond.SetResult();
 var finished=(List<OrderDocumentDraft>)((IValueHttpResult)await streamedConversion).Value!;
 hub.HoldSecond=null;
 Check(finished.Count==2 && finished[0].ImportRowId==early.ImportRowId && finished[1].ImportRowId!=early.ImportRowId &&
     finished.All(d=>d.ImportId==early.ImportId),"completion preserves IDs of incremental drafts and a single import batch");
 hub.FailSecond=true;
 var partial=await conversion.Convert(pdfSample,null,default,deferAddressLookup:true);
 var partialDrafts=(partial as IValueHttpResult)?.Value as List<OrderDocumentDraft>;
 Check(partialDrafts is [{ExtractionError:null},{ExtractionError:not null}] && partialDrafts[1].SourcePages.SequenceEqual(new[]{2}),
     "a bad extraction on one shipment preserves the other issuer's draft");
 var partialHistory=(await history.List(default)).First();
 Check(partialHistory.FailedCount==1 && (await history.Failures(partialHistory.Id,default)).Single().OrderLabel=="PDF pages 2",
     "a failed shipment retains its source page and reason in Recent imports");
 hub.FailSecond=false;
 hub.PacketFixture=false;
 context.Company=b;
 await conversion.Convert(sample,null,default);
 Check(hub.Prompt.Contains("Return ONLY a JSON array") && !hub.Prompt.Contains(companyInstructions) && hub.Credential==b.ToString(),"a cleared tenant uses the shared prompt and its own Hub credential");
 Check(hub.Prompt.Contains(globalRules),"global rules apply even when a tenant clears company instructions");
 hub.Status=System.Net.HttpStatusCode.ServiceUnavailable;
 var unsuccessful=await conversion.Convert(sample,null,default);
 var returnedReason=((unsuccessful as IValueHttpResult)?.Value as ApiErrorDto)?.Error;
 var failedFile=(await history.List(default)).First();
 Check(failedFile.Status=="Failed" && failedFile.FailureReason==returnedReason && returnedReason?.Contains("503")==true,"conversion records the same Hub failure returned to the reviewer");
 context.Company=a;
 await using(var db=await factory.OpenConnectionAsync())
 {
  var json=await db.ExecuteScalarAsync<string>("SELECT public.aurora_tenant_nova_connection()");
  Check(nova.Resolve(nova.Read(json),a).Enabled,"tenant connection available without per-user database assignment");
 }
 Check(await owner.ExecuteScalarAsync<bool>("SELECT NOT EXISTS(SELECT 1 FROM information_schema.columns WHERE table_name='user_tenant' AND column_name='nova_database_id')"),"old user database assignment column removed");
}
finally
{
 foreach(var id in new[]{a,b})await owner.ExecuteAsync("""
 DELETE FROM order_import_history WHERE tenant_id=@id;
 DELETE FROM aurora_sync_connection WHERE tenant_id=@id;
 DELETE FROM tenant_settings WHERE tenant_id=@id;
 DELETE FROM "AspNetUserRoles" WHERE "TenantId"=@id;
 DELETE FROM user_tenant WHERE tenant_id=@id;
 DELETE FROM tenant_product WHERE tenant_id=@id;
 DELETE FROM tenant WHERE id=@id;
 """,new{id});
}
Console.WriteLine($"{count} tenant settings checks passed; fixtures removed.");
sealed class SampleHub : HttpMessageHandler, IHttpClientFactory
{
 public System.Net.HttpStatusCode Status {get;set;}=System.Net.HttpStatusCode.OK;
 public TaskCompletionSource? HoldSecond {get;set;}
 public int IndexCalls {get;private set;}
 public bool FailSecond {get;set;}
 public bool SeparatePro {get;set;}
 public bool CopyFixture {get;set;}
 public bool PacketFixture {get;set;}
 public bool ScopedPromptSeen {get;private set;}
 public bool OtherPromptIsolated {get;private set;}
 public string Prompt {get;private set;}="";
 public string? Credential {get;private set;}
 public HttpClient CreateClient(string name)=>new(this,disposeHandler:false);
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
 {
  var multipart=(MultipartFormDataContent)request.Content!;
  var prompt=await multipart.Single(p=>p.Headers.ContentDisposition?.Name?.Trim('"')=="prompt").ReadAsStringAsync(ct);
  Prompt=prompt;
  Credential=request.Headers.Authorization?.Parameter;
  if(PacketFixture)
  {
   string body;
   if(prompt.StartsWith("Inventory"))IndexCalls++;
   if(CopyFixture && prompt.StartsWith("Inventory"))
   {
    if(prompt.Contains("There are 5 pages"))
     body="""[{"page":1,"kind":"support"},{"page":2,"kind":"support"},{"page":3,"kind":"support"},{"page":4,"kind":"support"},{"page":5,"kind":"order","issuer":"Alliance Inc.","reference":"J24083","purchaseOrder":"55817","consignee":"Longines Boutique","pieces":1,"weightLb":105,"number":"123762"}]""";
    else body="""[{"page":1,"kind":"order","issuer":"Alliance Inc."},{"page":2,"kind":"order","issuer":"Alliance Inc","reference":"J24083","purchaseOrder":"55817","consignee":"Longines Boutique","pieces":1,"weightLb":105},{"page":3,"kind":"order","issuer":"Other issuer","number":"888"}]""";
   }
   else if(prompt.StartsWith("Inventory"))
    body="""[{"page":1,"kind":"order","issuer":"Lykes Cartage Company","title":"Delivery Only","number":"MCO3735721","account":"UPS9001 - UPS SUPPLY CHAIN / MCO - MCO","lykesAccountForm":true},{"page":2,"kind":"order","issuer":"Unrelated issuer","title":"Delivery Only","number":"888","account":"UPS9001"}]""";
   else if(prompt.Contains("original PDF pages 1."))
   {
    ScopedPromptSeen=prompt.Contains("Use the customer Account header.") && prompt.Contains("Lykes freight bill:");
    body="""[{"PrintedBillToAccountLine":"UPS9001 - UPS SUPPLY CHAIN / MCO - MCO","PrintedLykesTag":"MCO3735721","HouseAirWaybill":"MCO3735721","TerminalCode":"MCO","Order":{"Customer":"UPS AIR CARGO"}}]""";
   }
   else
   {
    if(HoldSecond is {} gate)await gate.Task.WaitAsync(ct);
    OtherPromptIsolated=!prompt.Contains("Use the customer Account header.") && !prompt.Contains("Lykes freight bill:");
    body=FailSecond ? "[]" : SeparatePro
        ? """[{"BillOfLading":"MAV00042","PrintedIdentifiers":[{"Label":"PRO Number","Value":"3456789"}],"Order":{"Customer":"Receiver"}}]"""
        : """[{"BillOfLading":"MAV00042","PrintedIdentifiers":[{"Label":"Bill No.","Value":"MAV00042"}],"Order":{"Id":"42","Customer":"Receiver"}}]""";
   }
   return new(Status){Content=new StringContent(body)};
  }
  return new(Status){Content=new StringContent("""[{"TagNumber":"MCO42","Order":{"Customer":"Receiver"}}]""")};
 }
}

sealed class Context(Guid company,Guid user):ITenantContext
{
 public Guid Company=company,User=user;
 public Guid? TenantIdOrNull=>Company;
 public Guid? UserIdOrNull=>User;
}
