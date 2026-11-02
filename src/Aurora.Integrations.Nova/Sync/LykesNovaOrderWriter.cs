using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Aurora.Modules.Sync;
using Dapper;
using FirebirdSql.Data.FirebirdClient;

namespace Aurora.Integrations.Nova.Sync;

// Order lifecycle shares the provider's transaction/receipt. Never opens a second transaction.
public static class LykesNovaOrderWriter
{
    public static void RequireEnabled(LykesNovaOptions options)
    {
        if(options.NativeReadOnly||!options.Enabled||!options.WritesEnabled||!options.OrderLifecycleEnabled)
            throw new FormatException("Nova Lykes order creation/cancellation is disabled.");
        if(string.IsNullOrWhiteSpace(options.OperatorInitials)||options.OperatorInitials.Length>8||options.OperatorInitials!=options.OperatorInitials.Trim())
            throw new FormatException("Configure native operator initials (1–8 characters) for lifecycle audit.");
        LykesNovaMapping.ValidateOptions(options);
    }

    public static async Task<long> Create(FbConnection db,FbTransaction tx,JsonObject data,LykesNovaOptions options,
        IReadOnlyCollection<int> readyStatuses,Guid consumerId,Guid changeId,TimeZoneInfo zone,CancellationToken ct)
    {
        RequireEnabled(options);
        LykesNovaMapping.RequireSupportedWrite("order",data,options);
        var order=data.Deserialize<TmsOrder>()??throw new FormatException("Order required.");
        if(order.Status=="Ready")order.Status=WorkspaceOrderStatus.Ready; // legacy Lykes alias; do not rewrite the payload
        order.Validate();
        if(order.WorkType!="Delivery"||order.Status!=WorkspaceOrderStatus.Ready||string.IsNullOrWhiteSpace(order.Shipper)||order.NativeOrderId is not null||
            order.ManifestId is not null||!string.IsNullOrEmpty(order.ManifestNumber)||data["FreightOpsParent"] is not null||
            data["FreightOpsState"]?.GetValue<string>() is not (null or "" or "Created"))
            throw new FormatException("Create only new, unassigned, ready delivery orders with shipper and consignee.");
        if(!long.TryParse(order.Id,NumberStyles.None,CultureInfo.InvariantCulture,out var pro)||pro is <1 or >999999999||order.Id!=pro.ToString(CultureInfo.InvariantCulture))
            throw new FormatException("New Nova orders need a canonical positive numeric PRO of at most nine digits.");
        var terminal=options.TerminalMappings.Where(x=>x.Value==order.TerminalCode).Select(x=>x.Key).SingleOrDefault()
            ??throw new FormatException("Map the shared terminal to Nova before creating orders.");
        if(await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM LOCMAST WHERE LM_ID=@terminal",new{terminal},tx,cancellationToken:ct))!=1)
            throw new FormatException("Mapped Nova terminal does not exist.");
        var ready=options.NewOrderReadyStatus??(readyStatuses.Count==1?readyStatuses.Single():0);
        if(ready<=0||!readyStatuses.Contains(ready)||await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM CT_STAT WHERE CTS_ID=@ready AND TRIM(CTS_CODE) NOT IN ('VOID','CPLT','ENR','DPCH')",new{ready},tx,cancellationToken:ct))!=1)
            throw new FormatException("Select an unambiguous native ready status for new orders.");
        if(await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME='AURORA_SYNC_ORDER_KEY'",transaction:tx,cancellationToken:ct))!=1)
            throw new FormatException("Install the additive order-key migration before order creation.");
        if(await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM OPS_HEADER WHERE PB_TYPE='O' AND PBNUM=@pro",new{pro},tx,cancellationToken:ct))!=0||
           await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM AURORA_SYNC_ORDER_KEY WHERE PRO_NUMBER=@pro",new{pro},tx,cancellationToken:ct))!=0)
            throw new SyncConflictException("This PRO already exists or is reserved, including cancelled orders. Import it instead.");
        if(!string.IsNullOrWhiteSpace(order.CustomerCode)&&await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM ACCOUNT WHERE AC_CUST=@code",new{code=order.CustomerCode},tx,cancellationToken:ct))!=1)
            throw new FormatException("The supplied Nova consignee account does not exist.");
        var id=await FirebirdRoutingSource.NextId(db,tx,true,ct);
        try
        {
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO AURORA_SYNC_ORDER_KEY(PRO_NUMBER,ORDER_ID) VALUES(@pro,@id)",new{pro,id},tx,cancellationToken:ct));
        }
        catch(FbException e) when(e.SQLSTATE?.StartsWith("23",StringComparison.Ordinal)==true)
        {
            throw new SyncConflictException("Another sync request reserved this PRO. Import and reconcile the existing order.");
        }
        var now=TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,zone).DateTime;
        await db.ExecuteAsync(new CommandDefinition("""
            INSERT INTO OPS_HEADER(PB_ID,PB_TYPE,PB_LOAD_TYPE,PBNUM,PBLOCAT,PBSUFFIX,PB_LOCATION,PB_STATUS,PB_DT_ENT,PBINIT,
              PB_STATUS_TIME,PB_EXTERNAL_REF,PBBOL,PBBNAME,PBBADDR1,PBBADDR2,PBBCITY,PBBSTATE,PBBZIP,PBBCOUNTRY,PB_NOTES,
              PBTPIECE,PBTWEIGH,PB_TTL_CUBE,PBTOTBIL,PBCREV,PB_TTL_PALLETS,PB_TTL_PALLET_POSN)
            VALUES(@id,'O','O',@pro,@terminal,'',@terminal,@ready,@now,@initials,@now,@syncReference,@Reference,
              @BillTo,@BillToAddress,@BillToAddress2,@BillToCity,@BillToState,@BillToPostalCode,@BillToCountry,@Notes,0,0,0,0,0,0,0)
            """,new{id,pro,terminal,ready,now,initials=options.OperatorInitials,syncReference=$"aurora-sync:{changeId:N}",
                order.Reference,order.BillTo,order.BillToAddress,order.BillToAddress2,order.BillToCity,order.BillToState,order.BillToPostalCode,order.BillToCountry,order.Notes},tx,cancellationToken:ct));
        foreach(var kind in new[]{"P","D"})
        {
            var stop=await FirebirdRoutingSource.NextId(db,tx,false,ct);var pickup=kind=="P";
            var low=pickup?(DateTime?)null:TimeZoneInfo.ConvertTime(order.AppointmentFrom??order.ScheduledAt,zone).DateTime;
            var high=pickup?(DateTime?)null:TimeZoneInfo.ConvertTime(order.AppointmentTo??order.ScheduledAt,zone).DateTime;
            await db.ExecuteAsync(new CommandDefinition("""
                INSERT INTO OPS_STOP_REC(OPS_STOP_ID,OPS_ORDER_ID,OPS_STOP_NUMBER,OPS_TYPE,OPS_RATE,OPS_NAME,OPS_ADDR1,OPS_ADDR2,
                  OPS_CITY,OPS_STATE,OPS_ZIP,OPS_COUNTRY,OPS_ACCTID,OPS_LATITUDE,OPS_LONGITUDE,OPS_APPT_LOW,OPS_APPT_HIGH,
                  OPS_TTL_PIECES,OPS_TTL_WEIGHT,OPS_TTL_CUBE,OPS_TTL_PALLETS,OPS_TTL_PALLET_POSN)
                VALUES(@stop,@id,@sequence,@kind,@rate,@name,@address,@address2,@city,@state,@zip,@country,@account,@lat,@lon,@low,@high,0,0,0,0,0)
                """,new{stop,id,sequence=pickup?1:2,kind,rate=kind==options.FreightStopKind?1:0,
                    name=pickup?order.Shipper:order.Customer,address=pickup?order.OriginAddress:order.Address,address2=pickup?order.OriginAddress2:order.Address2,
                    city=pickup?order.OriginCity:order.City,state=pickup?order.OriginState:order.State,zip=pickup?order.OriginPostalCode:order.PostalCode,
                    country=pickup?order.OriginCountry:order.Country,account=pickup?null:order.CustomerCode,
                    lat=pickup?order.OriginLatitude:order.Latitude,lon=pickup?order.OriginLongitude:order.Longitude,low,high},tx,cancellationToken:ct));
        }
        await LykesNovaLineWriter.Seed(db,tx,id,data,options,readyStatuses,consumerId,ct);
        return id;
    }

    sealed class CancelHeader
    {
        public int Status {get;set;} public string Terminal {get;set;}="";public DateTime? Cancelled {get;set;}
        public DateTime? Rated {get;set;} public DateTime? Audited {get;set;} public DateTime? Billed {get;set;} public DateTime? Invoice {get;set;}
        public decimal Bill {get;set;} public decimal Revenue {get;set;} public decimal Paid {get;set;} public decimal Payable {get;set;} public decimal Receivable {get;set;}
        public decimal Discount {get;set;} public decimal DiscountPercent {get;set;} public decimal Cod {get;set;}
        public long Master {get;set;} public string? RatingNotes {get;set;} public string? NoCharge {get;set;}
    }
    sealed class CancelStop {public long Id {get;set;} public string Kind {get;set;}="";public long? Manifest {get;set;} public DateTime? Delivered {get;set;}}

    public static async Task Cancel(FbConnection db,FbTransaction tx,long id,JsonObject data,JsonObject baseline,LykesNovaOptions options,
        IReadOnlyCollection<int> readyStatuses,Guid consumerId,Guid changeId,TimeZoneInfo zone,CancellationToken ct)
    {
        RequireEnabled(options);
        LykesNovaMapping.RequireSupportedWrite("order",data,options);
        if(data["FreightOpsParent"] is not null||data["FreightOpsState"]?.GetValue<string>() is not (null or "" or "Created" or "Voided")||
           data["Status"]?.GetValue<string>() is not ("Ready" or WorkspaceOrderStatus.Ready or "Voided"))throw new SyncConflictException("Only unhandled orders can be cancelled through sync.");
        LykesNovaLineWriter.Unchanged(data,baseline,"Status","FreightOpsState","Lykes","CancellationReason");
        var profile=data["Lykes"] as JsonObject??throw new FormatException("Complete cancellation snapshot required.");
        var prior=baseline["Lykes"]!.AsObject();LykesNovaLineWriter.Unchanged(profile,prior,"Lines");
        var expected=prior["Lines"]!.AsArray().OfType<JsonObject>().ToDictionary(x=>Guid.Parse(x["LineId"]!.GetValue<string>()));
        var rows=profile["Lines"] as JsonArray??throw new FormatException("Cancellation must retain line identities.");var seen=new HashSet<Guid>();
        foreach(var node in rows)
        {
            var row=node as JsonObject??throw new FormatException("Invalid cancellation line.");
            if(!Guid.TryParse(row["LineId"]?.GetValue<string>(),out var key)||!seen.Add(key)||!expected.TryGetValue(key,out var old))throw new FormatException("Cancellation cannot replace lines.");
            LykesNovaLineWriter.Unchanged(row,old);
        }
        if(seen.Count!=expected.Count)throw new FormatException("Cancellation retains all freight and charges; it does not delete lines.");
        var header=await db.QuerySingleOrDefaultAsync<CancelHeader>(new CommandDefinition("""
            SELECT PB_STATUS AS Status,TRIM(PB_LOCATION) AS Terminal,PB_DT_CANC AS Cancelled,PB_DT_RATE AS Rated,PB_DT_AUD AS Audited,
              PB_DT_BILL AS Billed,PB_INV_DATE AS Invoice,COALESCE(PBTOTBIL,0) AS Bill,COALESCE(PBCREV,0) AS Revenue,
              COALESCE(PBTOTPD,0) AS Paid,COALESCE(PBILPAY,0) AS Payable,COALESCE(PBILREC,0) AS Receivable,
              COALESCE(PB_DISCOUNT_AMT,0) AS Discount,COALESCE(PB_DISCOUNT_PERC,0) AS DiscountPercent,COALESCE(PB_COD_AMT,0) AS Cod,
              COALESCE(PB_MASTER_ID,0) AS Master,PB_RATE_NOTES AS RatingNotes,PB_NO_CHRG AS NoCharge
            FROM OPS_HEADER WHERE PB_ID=@id AND PB_TYPE='O' WITH LOCK
            """,new{id},tx,cancellationToken:ct))??throw new SyncConflictException("Nova order disappeared.");
        if(header.Cancelled is not null)return; // Already cancelled; never clear or rewrite its native audit.
        if(!readyStatuses.Contains(header.Status)||header.Master!=0||header.Rated is not null||header.Audited is not null||header.Billed is not null||header.Invoice is not null||
            header.Bill!=0||header.Revenue!=0||header.Paid!=0||header.Payable!=0||header.Receivable!=0||header.Discount!=0||header.DiscountPercent!=0||header.Cod!=0||header.NoCharge?.Trim()=="Y"||!string.IsNullOrWhiteSpace(header.RatingNotes))
            throw new SyncConflictException("Rated, posted, commercial or consolidated orders require native cancellation review.");
        var stops=(await db.QueryAsync<CancelStop>(new CommandDefinition("""
            SELECT OPS_STOP_ID AS Id,TRIM(OPS_TYPE) AS Kind,OPS_MANIFEST_NUMBER AS Manifest,OPS_DELIVERED_TIME AS Delivered
            FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@id WITH LOCK
            """,new{id},tx,cancellationToken:ct))).ToArray();
        if(stops.Length!=2||stops.Count(x=>x.Kind=="P")!=1||stops.Count(x=>x.Kind=="D")!=1||stops.Any(x=>x.Manifest is >0||x.Delivered is not null))
            throw new SyncConflictException("Assigned, delivered or multi-stop freight requires native cancellation review.");
        if((await db.QueryAsync<long>(new CommandDefinition("SELECT OSM_PKUP_DELV_ID FROM OPS_MANIFEST_STOPS WHERE OSM_PKUP_DELV_ID IN @stops WITH LOCK",
            new{stops=stops.Select(x=>x.Id).ToArray()},tx,cancellationToken:ct))).Any())throw new SyncConflictException("Remove actual native manifest memberships before cancellation.");
        await db.QueryAsync<int>(new CommandDefinition("SELECT OPL_SEQUENCE FROM OPS_LINE_ITEMS WHERE OPL_ORDER_ID=@id WITH LOCK",new{id},tx,cancellationToken:ct));
        var source=(JsonObject)baseline.DeepClone();source["TerminalCode"]=header.Terminal;
        var current=LykesNovaMapping.Project(source,await LykesNovaMapping.ReadLines(db,tx,id,ct),options);
        if(!SyncJson.Equal(current["Lykes"],baseline["Lykes"])||!SyncJson.Equal(current["TerminalCode"],baseline["TerminalCode"]))throw new SyncConflictException("Nova freight changed before cancellation; import and review.");
        if(await db.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM OPS_LINE_ITEMS WHERE OPL_ORDER_ID=@id AND
              (COALESCE(OPL_RATE,0)<>0 OR COALESCE(OPL_AMOUNT,0)<>0 OR COALESCE(OPL_DIM_WGT,0)<>0 OR COALESCE(TRIM(OPL_RATE_TYPE),'')<>'')
            """,new{id},tx,cancellationToken:ct))!=0)throw new SyncConflictException("Rated lines require native cancellation review.");
        var voidStatuses=(await db.QueryAsync<int>(new CommandDefinition("SELECT CTS_ID FROM CT_STAT WHERE TRIM(CTS_CODE)='VOID'",transaction:tx,cancellationToken:ct))).ToArray();
        if(voidStatuses.Length!=1)throw new FormatException("A unique native VOID status is required.");
        var reason=data["CancellationReason"]?.GetValue<string>()?.Trim();
        if(reason?.Length>500)throw new FormatException("Cancellation reason allows 500 characters.");
        reason=string.IsNullOrWhiteSpace(reason)?$"Cancelled by Aurora sync ({changeId:D})":$"{reason} [Aurora sync {changeId:D}]";
        var now=TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,zone).DateTime;
        await db.ExecuteAsync(new CommandDefinition("""
            UPDATE OPS_HEADER SET PB_DT_CANC=@now,PB_STATUS=@status,PB_STATUS_TIME=@now,PBCINIT=@initials,PB_RATE_NOTES=@reason
            WHERE PB_ID=@id
            """,new{id,now,status=voidStatuses[0],initials=options.OperatorInitials,reason},tx,cancellationToken:ct));
        await db.ExecuteAsync(new CommandDefinition("""
            INSERT INTO AURORA_SYNC_EVENT(EVENT_ID,CONSUMER_ID,ENTITY_TYPE,ENTITY_ID,CHANGE_KIND)
            VALUES(GEN_ID(AURORA_SYNC_EVENT_GEN,1),@consumer,'order',@id,'D')
            """,new{consumer=consumerId.ToString(),id=id.ToString(CultureInfo.InvariantCulture)},tx,cancellationToken:ct));
    }
}
