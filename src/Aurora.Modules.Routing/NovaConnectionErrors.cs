namespace Aurora.Modules.Routing;

public static class NovaConnectionErrors
{
    public static string UnsupportedPlugin(NotSupportedException error) => error.Message switch
    {
        "Not supported plugin 'srp'." => "The Nova server reports authentication plugin 'srp'. The Firebird driver requires 'Srp' (case-sensitive). Correct the plugin spelling in the server's AuthServer setting, then test again.",
        "Not supported plugin 'srp256'." => "The Nova server reports authentication plugin 'srp256'. The Firebird driver requires 'Srp256' (case-sensitive). Correct the plugin spelling in the server's AuthServer setting, then test again.",
        _ => "The Nova server requested an authentication plugin that this Firebird driver does not support. Check the server's authentication configuration."
    };
}
