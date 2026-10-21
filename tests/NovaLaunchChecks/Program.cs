using Aurora.Api.Features.Auth;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using System.Security.Cryptography;

int checks=0;
void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;}
Check(!NovaLaunch.ValidTicket(null) && !NovaLaunch.ValidTicket("") && !NovaLaunch.ValidTicket(new string('a',63)),"missing and short tickets rejected");
Check(!NovaLaunch.ValidTicket(new string('A',64)) && !NovaLaunch.ValidTicket(new string('z',64)),"noncanonical tickets rejected");
var ticket=Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
Check(NovaLaunch.ValidTicket(ticket),"256-bit ticket accepted");
Check(NovaLaunch.TicketHash("abc")=="ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad","SHA-256 contract");

// Create a disposable embedded Firebird database; never connect to a Nova database.
var path=Path.Combine(Path.GetTempPath(),"aurora-launch-check-"+Guid.NewGuid().ToString("N")+".fdb");
var cs=new FbConnectionStringBuilder { Database=path, UserID="SYSDBA", Password="test", ServerType=FbServerType.Embedded,
    ClientLibrary=Environment.GetEnvironmentVariable("FIREBIRD_CLIENT_LIBRARY")??@"C:\Program Files\Firebird\Firebird_5_0\fbclient.dll", Pooling=false }.ConnectionString;
FbConnection.CreateDatabase(cs);
try
{
    await using var fb=new FbConnection(cs);await fb.OpenAsync();
    await fb.ExecuteAsync("CREATE TABLE AURORA_LAUNCH_TICKET(TICKET_HASH CHAR(64) NOT NULL PRIMARY KEY,NOVA_USER_ID VARCHAR(128) NOT NULL,CREATED_AT TIMESTAMP NOT NULL,EXPIRES_AT TIMESTAMP NOT NULL)");
    async Task Insert(string token,int age,int lifetime)=>await fb.ExecuteAsync("INSERT INTO AURORA_LAUNCH_TICKET VALUES(@hash,'NOVA_TEST',DATEADD(SECOND,@age,CURRENT_TIMESTAMP),DATEADD(SECOND,@expiry,CURRENT_TIMESTAMP))",new{hash=NovaLaunch.TicketHash(token),age,expiry=age+lifetime});
    await Insert(ticket,0,60);
    Check(await NovaLaunch.Consume(fb,NovaLaunch.TicketHash(ticket),default)=="NOVA_TEST","valid ticket returns identity");
    Check(await NovaLaunch.Consume(fb,NovaLaunch.TicketHash(ticket),default)==null,"replay rejected");
    ticket=new string('b',64);await Insert(ticket,-120,60);
    Check(await NovaLaunch.Consume(fb,NovaLaunch.TicketHash(ticket),default)==null,"expired ticket rejected");
    ticket=new string('c',64);await Insert(ticket,0,120);
    Check(await NovaLaunch.Consume(fb,NovaLaunch.TicketHash(ticket),default)==null,"excessive lifetime rejected");
    ticket=new string('d',64);await Insert(ticket,120,60);
    Check(await NovaLaunch.Consume(fb,NovaLaunch.TicketHash(ticket),default)==null,"future-issued ticket rejected");
    ticket=new string('e',64);await Insert(ticket,0,60);
    async Task<bool> Race(){await using var other=new FbConnection(cs);await other.OpenAsync();try{return await NovaLaunch.Consume(other,NovaLaunch.TicketHash(ticket),default)!=null;}catch(FbException){return false;}}
    var race=await Task.WhenAll(Race(),Race());
    Check(race.Count(x=>x)==1,"concurrent redemption has exactly one winner");
}
finally { FbConnection.DropDatabase(cs); }
Console.WriteLine($"{checks} Nova launch checks passed.");
if(Environment.GetEnvironmentVariable("NOVA_LAUNCH_PG") is {Length:>0} pg) await MappingChecks.Run(pg);
if(Environment.GetEnvironmentVariable("NOVA_LAUNCH_PG") is {Length:>0} owner && Environment.GetEnvironmentVariable("NOVA_LAUNCH_APP_PG") is {Length:>0} appConnection) await ApiChecks.Run(owner,appConnection);
