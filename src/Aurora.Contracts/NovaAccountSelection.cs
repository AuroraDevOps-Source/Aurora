namespace Aurora.Contracts;

public static class NovaAccountSelection
{
    public static Dictionary<string,string?> Values(NovaMasterDocument account, bool billTo)
    {
        var source=account.Fields.ToDictionary(f=>f.Key,f=>f.Value);
        var values=new Dictionary<string,string?>();
        void Copy(string target,string key)=>values[target]=source.GetValueOrDefault(key);
        var prefix=billTo?"PBB":"OPS_";
        foreach(var key in new[]{"NAME","ADDR1","ADDR2","CITY","STATE","ZIP"})
            Copy(prefix+key,"AC_"+key);
        Copy(prefix+"COUNTRY","AC_COUNTRY_CD");
        values[billTo?"PB_BILL_ID":"OPS_ACCTID"]=account.Id;
        if(!billTo)
        {
            Copy("OPS_ADDR3","AC_ADDR3");Copy("OPS_PHONE","AC_PHONE");
            values["OPS_CONTACT"]=null;values["OPS_CONTACT_EMAIL"]=null;
            // The save path resolves coordinates for the selected account's address.
            values["OPS_LATITUDE"]=null;values["OPS_LONGITUDE"]=null;
        }
        return values;
    }
}
