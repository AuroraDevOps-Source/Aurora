using System.Globalization;
using Microsoft.AspNetCore.Components.Forms;

namespace Aurora.Client;

public sealed class MoneyInput : InputNumber<decimal>
{
    protected override string FormatValueAsString(decimal value) =>
        value.ToString("F2", CultureInfo.InvariantCulture);
}
