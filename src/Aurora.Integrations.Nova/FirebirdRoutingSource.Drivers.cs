using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aurora.Contracts;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

using Aurora.Modules.Routing;

namespace Aurora.Integrations.Nova;
public sealed partial class FirebirdRoutingSource
{
    private const string DriverSql="""
        SELECT e.EH_ID AS Id,TRIM(e.EH_UNIT_ID) AS Number,COALESCE(TRIM(e.EH_DOMICILE),'') AS Terminal,
            COALESCE(TRIM(d.DH_FIRST_NAME),'') AS FirstName,COALESCE(TRIM(d.DH_LAST_NAME),'') AS LastName,
            COALESCE(TRIM(d.DH_CELL_PHONE),'') AS Phone,COALESCE(TRIM(d.DH_EMAIL),'') AS Email,
            d.DH_HIRE_DATE AS HireDate,d.DH_TERM_DATE AS TerminationDate,d.DH_DL_EXPIRY AS LicenseExpiry,
            d.DH_ASG_TRACTOR AS TruckId,COALESCE(TRIM(truck.EH_UNIT_ID),'') AS Truck,
            COALESCE(TRIM(d.DH_STD_START_TIME),'') AS ShiftStart,COALESCE(TRIM(d.DH_STD_END_TIME),'') AS ShiftEnd,
            d.DH_FULL_BURDENED_HR AS PerHour
        FROM EQUIP_HEADER e JOIN CT_TYPE t ON t.CTT_ID=e.EH_UNIT_TYPE
        LEFT JOIN DRIVER_HEADER d ON d.DH_ID=e.EH_ID LEFT JOIN EQUIP_HEADER truck ON truck.EH_ID=d.DH_ASG_TRACTOR
        WHERE e.EH_DELETE_DATE IS NULL AND t.CTT_CODE='DRV'
        """;
    private static async Task<NovaDriver[]> Drivers(FbConnection db,FbTransaction tx,CancellationToken ct)
    {
        var rows=(await db.QueryAsync<NovaDriver>(new CommandDefinition(DriverSql+" ORDER BY e.EH_UNIT_ID",transaction:tx,cancellationToken:ct))).ToArray();
        foreach(var row in rows)
        {
            string Clock(string s)=>s.Length==4?s[..2]+":"+s[2..]:s;
            row.ShiftStart=Clock(row.ShiftStart);row.ShiftEnd=Clock(row.ShiftEnd);
            row.Revision=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(row))));
        }
        return rows;
    }
    public async Task<DriverDirectory> ReadDrivers(CancellationToken ct)
    {
        await using var db=await Open(ct);
        await using var tx=db.BeginTransaction(new FbTransactionOptions{TransactionBehavior=FbTransactionBehavior.Read|FbTransactionBehavior.Concurrency|FbTransactionBehavior.Wait});
        var drivers=await Drivers(db,tx,ct);
        var trucks=(await db.QueryAsync<DriverTruck>(new CommandDefinition("SELECT e.EH_ID AS Id,TRIM(e.EH_UNIT_ID) AS Number,COALESCE(TRIM(e.EH_DOMICILE),'') AS Terminal FROM EQUIP_HEADER e JOIN CT_TYPE t ON t.CTT_ID=e.EH_UNIT_TYPE WHERE e.EH_DELETE_DATE IS NULL AND t.CTT_CODE='TRK' ORDER BY e.EH_UNIT_ID",transaction:tx,cancellationToken:ct))).ToArray();
        var terminals=(await db.QueryAsync<string>(new CommandDefinition("SELECT TRIM(LM_ID) FROM LOCMAST ORDER BY LM_ID",transaction:tx,cancellationToken:ct))).ToArray();
        return new(drivers,trucks,terminals);
    }
    public async Task<NovaDriver> SaveDriver(long id,NovaDriver input,CancellationToken ct,bool rollbackOnly=false)
    {
        if(id<0)throw new FormatException("Invalid driver ID.");
        input.Validate();await using var db=await Open(ct);await using var tx=BeginCreate(db);
        if(id>0)
        {
            await db.QueryAsync<long>(new CommandDefinition("SELECT EH_ID FROM EQUIP_HEADER WHERE EH_ID=@id WITH LOCK",new{id},tx,cancellationToken:ct));
            await db.QueryAsync<long>(new CommandDefinition("SELECT DH_ID FROM DRIVER_HEADER WHERE DH_ID=@id WITH LOCK",new{id},tx,cancellationToken:ct));
            var current=(await Drivers(db,tx,ct)).SingleOrDefault(d=>d.Id==id)??throw new KeyNotFoundException("Driver no longer exists.");
            if(current.Revision!=input.Revision)throw new FormatException("This driver changed. Refresh before saving.");
            if(current.Number!=input.Number.Trim())throw new FormatException("Driver number cannot be changed.");
        }
        await ValidTerminal(db,tx,input.Terminal,ct);
        if(id==0)
        {
            var type=await db.QuerySingleOrDefaultAsync<long?>(new CommandDefinition("SELECT CTT_ID FROM CT_TYPE WHERE CTT_CODE='DRV'",transaction:tx,cancellationToken:ct))??throw new FormatException("Nova needs a DRV equipment type.");
            if(await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM EQUIP_HEADER WHERE EH_UNIT_ID=@Number",input,tx,cancellationToken:ct))>0)throw new FormatException("That driver or equipment number already exists.");
            do{id=await db.ExecuteScalarAsync<long>(new CommandDefinition("SELECT GEN_ID(EQ_GEN,1) FROM RDB$DATABASE",transaction:tx,cancellationToken:ct));}while(await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM EQUIP_HEADER WHERE EH_ID=@id",new{id},tx,cancellationToken:ct))>0);
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO EQUIP_HEADER(EH_ID,EH_UNIT_ID,EH_UNIT_TYPE,EH_DOMICILE) VALUES(@id,@number,@type,@terminal)",new{id,number=input.Number.Trim(),type,terminal=input.Terminal.Trim()},tx,cancellationToken:ct));
        }
        else await db.ExecuteAsync(new CommandDefinition("UPDATE EQUIP_HEADER SET EH_DOMICILE=@terminal WHERE EH_ID=@id",new{id,terminal=input.Terminal.Trim()},tx,cancellationToken:ct));
        // Update only the editable profile columns; payroll, qualifications and personal identifiers stay in Nova.
        if(await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM DRIVER_HEADER WHERE DH_ID=@id",new{id},tx,cancellationToken:ct))==0)
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO DRIVER_HEADER(DH_ID) VALUES(@id)",new{id},tx,cancellationToken:ct));
        await db.ExecuteAsync(new CommandDefinition("""
            UPDATE DRIVER_HEADER SET DH_FIRST_NAME=@first,DH_LAST_NAME=@last,DH_CELL_PHONE=@phone,DH_EMAIL=@email,
                DH_HIRE_DATE=@hire,DH_TERM_DATE=@term,DH_DL_EXPIRY=@expiry,
                DH_FULL_BURDENED_HR=@rate WHERE DH_ID=@id
            """,new{id,first=input.FirstName.Trim(),last=input.LastName.Trim(),phone=input.Phone.Trim(),email=input.Email.Trim(),hire=input.HireDate?.Date,term=input.TerminationDate?.Date,expiry=input.LicenseExpiry?.Date,rate=input.PerHour},tx,cancellationToken:ct));
        var result=(await Drivers(db,tx,ct)).Single(d=>d.Id==id);
        if(rollbackOnly)await tx.RollbackAsync(ct);else await tx.CommitAsync(ct);
        return result;
    }
    public async Task DeleteDriver(long id,string revision,CancellationToken ct)
    {
        await using var db=await Open(ct);await using var tx=BeginCreate(db);
        await db.QueryAsync<long>(new CommandDefinition("SELECT EH_ID FROM EQUIP_HEADER WHERE EH_ID=@id WITH LOCK",new{id},tx,cancellationToken:ct));
        await db.QueryAsync<long>(new CommandDefinition("SELECT DH_ID FROM DRIVER_HEADER WHERE DH_ID=@id WITH LOCK",new{id},tx,cancellationToken:ct));
        var current=(await Drivers(db,tx,ct)).SingleOrDefault(d=>d.Id==id)??throw new KeyNotFoundException("Driver no longer exists.");
        if(current.Revision!=revision)throw new FormatException("This driver changed. Refresh before deleting.");
        if(await db.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM OPS_HEADER m LEFT JOIN CT_STAT s ON s.CTS_ID=m.PB_STATUS
            WHERE m.PB_TYPE='M' AND m.PB_DT_CANC IS NULL AND COALESCE(s.CTS_CODE,'') NOT IN ('CPLT','VOID','DONE')
                AND (m.PB_CURRENT_DRIVER=@id OR m.PB_PLANNED_DRIVER=@id OR m.PB_PLANNED_DRIVER2=@id
                    OR EXISTS(SELECT 1 FROM DRIVER_PKUP_DELV d WHERE d.DPD_LOAD_NUMBER=m.PB_ID AND d.DPD_ID=@id))
            """,new{id},tx,cancellationToken:ct))>0)throw new FormatException("This driver is assigned or reserved on an open manifest. Reassign it before deleting.");
        await db.ExecuteAsync(new CommandDefinition("UPDATE EQUIP_HEADER SET EH_DELETE_DATE=CURRENT_TIMESTAMP WHERE EH_ID=@id",new{id},tx,cancellationToken:ct));
        await tx.CommitAsync(ct);
    }
}
