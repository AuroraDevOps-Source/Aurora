using Aurora.Integrations.Nova;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Routing;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Options;
using Npgsql;

int checks=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;}
async Task Reject(Func<Task> action,string contains){try{await action();}catch(FormatException e)when(e.Message.Contains(contains)){Check(true,"rejects "+contains);return;}throw new Exception("Expected rejection: "+contains);}
string Json()=>"""
{"locations":[{"id":"DEPOT","latitude":40,"longitude":-80}],"vehicles":[{"id":"T1","start":{"locationId":"DEPOT","earliestStartTime":"2040-01-02T06:00:00-05:00"},"end":{"locationId":"DEPOT","latestEndTime":"2040-01-02T18:00:00-05:00"},"costs":{"perHour":20,"perKilometer":1}}],"reporting":{"terminal":"101","drivers":{"T1":{"id":"10","shiftStart":"08:00","shiftEnd":"17:00"}}}}
""";
var driver=new NovaDriver{Id=10,Number="D1",FirstName="Test",LastName="Driver",Terminal="101",TruckId=1,Truck="T1",ShiftStart="08:00",ShiftEnd="17:00",PerHour=30,Revision="one"};
string Apply(string basis)=>DriverPlanning.Apply(Json(),[driver],[],basis,true);
var separate=RoutingInput.Parse(Apply(DriverCostBasis.Separate));
Check(separate["vehicles"]![0]!["costs"]!["perHour"]!.GetValue<double>()==50,"separate labor adds to equipment rate");
Check(RoutingInput.Parse(Apply(DriverCostBasis.Included))["vehicles"]![0]!["costs"]!["perHour"]!.GetValue<double>()==50,"legacy included-cost choice cannot omit driver labor");
Check(RoutingInput.Time(separate["vehicles"]![0]!["start"]!["earliestStartTime"])!.Value.Hour==8&&RoutingInput.Time(separate["vehicles"]![0]!["end"]!["latestEndTime"])!.Value.Hour==17,"driver shift limits route hours");
PlannerOvertime.Allow(separate,separate["vehicles"]![0]!.AsObject());
Check(RoutingInput.Time(separate["vehicles"]![0]!["end"]!["latestEndTime"])!.Value.Hour==17,"manual assignments cannot bypass driver working hours");
var catalog=new EquipmentCatalogDto([new(){Code="T",Description="Truck",Weight=100,Cubes=100,PerHour=20,PerKilometer=1}],[new(){Id="T1",Terminal="101",TypeCode="T",Available=true,UseForRouting=true}]);
catalog.Units[0].DriverId=driver.Id;
var defaultsInput=RoutingInput.Parse(Json());
defaultsInput["reporting"]!["drivers"]=new JsonObject();
var defaults=RoutingInput.Parse(FleetPlanning.DefaultDrivers(defaultsInput.ToJsonString(),catalog));
Check(RoutingInput.Text(defaults["reporting"]!["drivers"]!["T1"]!["id"])==driver.Id.ToString(),"saved truck driver defaults into new plan");
Check(RoutingInput.Text(defaults["reporting"]!["drivers"]!["T1"]!["shiftStart"])==RoutingInput.Time(defaults["vehicles"]![0]!["start"]!["earliestStartTime"])!.Value.ToString("HH:mm"),"default driver shift follows plan schedule");
defaults["reporting"]!["drivers"]!["T1"]!["id"]="999";
Check(RoutingInput.Text(RoutingInput.Parse(FleetPlanning.DefaultDrivers(defaults.ToJsonString(),catalog))["reporting"]!["drivers"]!["T1"]!["id"])=="999","manual driver override survives default application");
catalog.Units[0].DriverId=null;
Check(!RoutingInput.Parse(FleetPlanning.DefaultDrivers(defaultsInput.ToJsonString(),catalog))["reporting"]!["drivers"]!.AsObject().Any(),"truck without default leaves driver choice empty");
var rebuilt=FleetPlanning.ApplySavedFleet(separate.ToJsonString(),catalog,"101");
Check(RoutingInput.Parse(DriverPlanning.Apply(rebuilt,[driver],[],DriverCostBasis.Separate,false))["vehicles"]![0]!["costs"]!["perHour"]!.GetValue<double>()==50,"rerun restores equipment before adding labor once");
await Reject(()=>Task.FromResult(DriverPlanning.Apply(Json(),[driver],[10],DriverCostBasis.Separate,true)),"reserved");
driver.PerHour=null;
await Reject(()=>Task.FromResult(Apply(DriverCostBasis.Separate)),"hourly labor cost");
await Reject(()=>Task.FromResult(Apply(DriverCostBasis.Included)),"hourly labor cost");
driver.PerHour=30;driver.TerminationDate=new(2040,1,2);
await Reject(()=>Task.FromResult(Apply(DriverCostBasis.Separate)),"Employment ends");
driver.TerminationDate=null;driver.LicenseExpiry=new(2040,1,1);
await Reject(()=>Task.FromResult(Apply(DriverCostBasis.Separate)),"License expires");
driver.LicenseExpiry=null;driver.Revision="two";
Check(RoutingInput.Text(RoutingInput.Parse(DriverPlanning.Apply(rebuilt,[driver],[],DriverCostBasis.Separate,false))["reporting"]!["drivers"]!["T1"]!["revision"])=="two","rerun refreshes driver snapshot from saved profile");
driver.Revision="one";driver.ShiftStart="22:00";driver.ShiftEnd="06:00";
var night=RoutingInput.Parse(Json());night["reporting"]!["drivers"]!["T1"]!["shiftStart"]="22:00";night["reporting"]!["drivers"]!["T1"]!["shiftEnd"]="06:00";night["vehicles"]![0]!["start"]!["earliestStartTime"]="2040-01-02T23:00:00-05:00";night["vehicles"]![0]!["end"]!["latestEndTime"]="2040-01-03T08:00:00-05:00";
Check(RoutingInput.Time(RoutingInput.Parse(DriverPlanning.Apply(night.ToJsonString(),[driver],[],DriverCostBasis.Separate,true))["vehicles"]![0]!["end"]!["latestEndTime"])!.Value.Hour==6,"overnight driver shift ends on next day");

driver.ShiftStart="08:00";driver.ShiftEnd="17:00";
var trip=MultiDayPlanning.Apply(Json(),new(2040,1,2),new(2040,1,4),"18:00",new(10,12,12));
var driverTrip=DriverPlanning.Apply(trip,[driver],[],DriverCostBasis.Separate,true);
Check(RoutingInput.Parse(MultiDayPlanning.ApplyRest(driverTrip))["vehicles"]![0]!["breaks"]!["working"]![0]!["maximumWorkingDuration"]!.GetValue<int>()==9*3600,"later request preparation retains driver working limit");

driver.Truck="";driver.TruckId=null;driver.ShiftStart="";driver.ShiftEnd="";
Check(RoutingInput.Parse(Apply(DriverCostBasis.Separate))["vehicles"]!.AsArray().Count==1,"planning assignment works without a saved truck or default hours");
var missing=RoutingInput.Parse(Json());missing["reporting"]!.AsObject().Remove("drivers");
await Reject(()=>Task.FromResult(DriverPlanning.Apply(missing.ToJsonString(),[driver],[],DriverCostBasis.Separate,false)),"Select a driver");
var duplicate=RoutingInput.Parse(Json());var other=duplicate["vehicles"]![0]!.DeepClone();other["id"]="T2";duplicate["vehicles"]!.AsArray().Add(other);duplicate["reporting"]!["drivers"]!["T2"]=duplicate["reporting"]!["drivers"]!["T1"]!.DeepClone();
await Reject(()=>Task.FromResult(DriverPlanning.Apply(duplicate.ToJsonString(),[driver],[],DriverCostBasis.Separate,false)),"cannot operate two trucks");
var changedShift=RoutingInput.Parse(Json());changedShift["reporting"]!["drivers"]!["T1"]!["shiftStart"]="10:00";
Check(RoutingInput.Time(RoutingInput.Parse(DriverPlanning.Apply(changedShift.ToJsonString(),[driver],[],DriverCostBasis.Separate,false))["vehicles"]![0]!["start"]!["earliestStartTime"])!.Value.Hour==10,"per-plan hours override defaults");

// Exercise native CRUD and reservation reads against a disposable embedded Firebird database.
var path=Path.Combine(Path.GetTempPath(),"aurora-drivers-"+Guid.NewGuid().ToString("N")+".fdb");
var cs=new FbConnectionStringBuilder{Database=path,UserID="SYSDBA",Password="test",ServerType=FbServerType.Embedded,ClientLibrary=@"C:\Program Files\Firebird\Firebird_5_0\fbclient.dll",Pooling=false}.ConnectionString;
FbConnection.CreateDatabase(cs);
try
{
    await using var db=new FbConnection(cs);await db.OpenAsync();
    foreach(var sql in new[]{
        "CREATE TABLE CT_TYPE(CTT_ID BIGINT PRIMARY KEY,CTT_CODE VARCHAR(12))",
        "CREATE TABLE LOCMAST(LM_ID VARCHAR(12) PRIMARY KEY)",
        "CREATE TABLE EQUIP_HEADER(EH_ID BIGINT PRIMARY KEY,EH_UNIT_ID VARCHAR(14),EH_UNIT_TYPE BIGINT,EH_DOMICILE VARCHAR(12),EH_DELETE_DATE TIMESTAMP)",
        "CREATE TABLE DRIVER_HEADER(DH_ID BIGINT PRIMARY KEY,DH_FIRST_NAME VARCHAR(40),DH_LAST_NAME VARCHAR(40),DH_CELL_PHONE VARCHAR(32),DH_EMAIL VARCHAR(150),DH_HIRE_DATE DATE,DH_TERM_DATE DATE,DH_DL_EXPIRY DATE,DH_ASG_TRACTOR BIGINT,DH_STD_START_TIME VARCHAR(4),DH_STD_END_TIME VARCHAR(4),DH_FULL_BURDENED_HR NUMERIC(9,4))",
        "CREATE TABLE CT_STAT(CTS_ID BIGINT PRIMARY KEY,CTS_CODE VARCHAR(12))",
        "CREATE TABLE OPS_HEADER(PB_ID BIGINT PRIMARY KEY,PB_TYPE CHAR(1),PB_DT_CANC TIMESTAMP,PB_STATUS BIGINT,PB_MFST_DATE DATE,PB_NOTES BLOB SUB_TYPE TEXT,PB_CURRENT_DRIVER BIGINT,PB_PLANNED_DRIVER BIGINT,PB_PLANNED_DRIVER2 BIGINT)",
        "CREATE TABLE DRIVER_PKUP_DELV(DPD_LOAD_NUMBER BIGINT,DPD_ID BIGINT)","CREATE SEQUENCE EQ_GEN START WITH 100"})await db.ExecuteAsync(sql);
    await db.ExecuteAsync("INSERT INTO CT_TYPE VALUES(1,'TRK')");await db.ExecuteAsync("INSERT INTO CT_TYPE VALUES(2,'DRV')");await db.ExecuteAsync("INSERT INTO LOCMAST VALUES('101')");
    await db.ExecuteAsync("INSERT INTO EQUIP_HEADER(EH_ID,EH_UNIT_ID,EH_UNIT_TYPE,EH_DOMICILE) VALUES(1,'T1',1,'101')");
    var tenant=new TestTenant(Guid.NewGuid());await using var pg=NpgsqlDataSource.Create("Host=localhost;Database=unused;Username=unused");
    var source=new FirebirdRoutingSource(new NpgsqlConnectionFactory(pg,tenant),tenant,Options.Create(new FirebirdRoutingOptions{Enabled=true,TenantId=tenant.TenantId,ConnectionString=cs,SourceKey="driver-check",TimeZoneId="America/New_York"}));
    driver.Id=0;driver.ShiftStart="08:00";driver.ShiftEnd="17:00";
    var created=await source.SaveDriver(0,driver,default);
    Check(created.Id>0&&string.IsNullOrEmpty(created.Truck)&&created.PerHour==30,"creates native equipment and driver profile together");
    Check((await source.ReadDrivers(default)).Drivers.Single().FirstName=="Test","reads driver directory and truck options");
    created.Phone="555-0100";var updated=await source.SaveDriver(created.Id,created,default);
    Check(updated.Phone=="555-0100"&&updated.Revision!=created.Revision,"updates profile and revision");
    await Reject(()=>source.SaveDriver(created.Id,created,default),"changed");
    await Reject(()=>source.SaveDriver(0,driver,default),"already exists");
    var chosen=RoutingInput.Parse(Json());chosen["reporting"]!["drivers"]!["T1"]!["id"]=created.Id.ToString();
    var planned=await source.ApplyDrivers(chosen.ToJsonString(),DriverCostBasis.Separate,true,default);
    Check(RoutingInput.Text(RoutingInput.Parse(planned)["reporting"]!["drivers"]!["T1"]!["number"])=="D1","native profile binds to planned truck");
    await db.ExecuteAsync("INSERT INTO CT_STAT VALUES(1,'UNAS')");
    await db.ExecuteAsync("INSERT INTO OPS_HEADER(PB_ID,PB_TYPE,PB_STATUS,PB_MFST_DATE,PB_PLANNED_DRIVER) VALUES(50,'M',1,'2040-01-02',@id)",new{id=created.Id});
    await Reject(()=>source.ApplyDrivers(chosen.ToJsonString(),DriverCostBasis.Separate,true,default),"reserved");
    await Reject(()=>source.DeleteDriver(updated.Id,updated.Revision,default),"open manifest");
    await db.ExecuteAsync("UPDATE OPS_HEADER SET PB_DT_CANC=CURRENT_TIMESTAMP WHERE PB_ID=50");
    await source.DeleteDriver(updated.Id,updated.Revision,default);
    Check((await source.ReadDrivers(default)).Drivers.Length==0,"deletion removes driver from active directory");
    Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DRIVER_HEADER")==1,"deletion preserves native driver history");
}
finally {FbConnection.ClearAllPools();FbConnection.DropDatabase(cs);}
Console.WriteLine($"{checks} driver checks passed.");
sealed class TestTenant(Guid id):ITenantContext{public Guid TenantId=>id;public Guid? TenantIdOrNull=>id;public Guid? UserIdOrNull=>null;}
