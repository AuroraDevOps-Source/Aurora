using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;


namespace Aurora.Api.Features.Routing;

internal static class OrderReportRows
{
    static readonly XNamespace Ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    internal static List<Dictionary<string,string>>? Read(Stream stream,string[] requiredHeaders,out bool date1904,bool daily=false)
    {
        using var zip=new ZipArchive(stream,ZipArchiveMode.Read,leaveOpen:true);
        if(zip.Entries.Sum(e=>e.Length)>100*1024*1024)throw new FormatException("The expanded workbook is too large.");
        XDocument? Xml(string path){var entry=zip.GetEntry(path);if(entry is null)return null;using var s=entry.Open();return XDocument.Load(s);}
        var shared=Xml("xl/sharedStrings.xml")?.Descendants(Ns+"si").Select(s=>string.Concat(s.Descendants(Ns+"t").Select(t=>t.Value))).ToArray()??[];
        date1904=(string?)Xml("xl/workbook.xml")?.Descendants(Ns+"workbookPr").FirstOrDefault()?.Attribute("date1904") is "1" or "true";
        var data=new List<Dictionary<string,string>>();bool recognized=false;
        foreach(var entry in zip.Entries.Where(e=>e.FullName.StartsWith("xl/worksheets/sheet",StringComparison.Ordinal)&&e.FullName.EndsWith(".xml",StringComparison.Ordinal)))
        {
            Dictionary<string,string>? headers=null;
            foreach(var row in Xml(entry.FullName)!.Descendants(Ns+"row"))
            {
                var cells=new Dictionary<string,string>();
                foreach(var c in row.Elements(Ns+"c"))
                {
                    var column=new string(((string?)c.Attribute("r")??"").TakeWhile(char.IsLetter).ToArray());
                    var value=(string?)c.Element(Ns+"v")??"";
                    if((string?)c.Attribute("t")=="s")value=shared[int.Parse(value,CultureInfo.InvariantCulture)];
                    else if((string?)c.Attribute("t")=="inlineStr")value=string.Concat(c.Descendants(Ns+"t").Select(t=>t.Value));
                    cells[column]=value.Trim();
                }
                if(headers is null)
                {
                    if(requiredHeaders.All(h=>cells.Values.Contains(h, daily ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)))
                    {headers=cells;recognized=true;}
                    continue;
                }
                var values=headers.Where(h=>h.Value.Length>0).ToDictionary(h=>h.Value,h=>cells.GetValueOrDefault(h.Key)??"",daily ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
                if(string.IsNullOrWhiteSpace(values["CID"]))
                {
                    if(daily && values.Values.Any(v=>v.Length>0))throw new FormatException($"Missing CID on worksheet row {row.Attribute("r")?.Value}.");
                    continue;
                }
                data.Add(values);
                if(data.Count>20000)throw new FormatException("Import up to 20,000 report rows at a time.");
            }
        }
        if(!recognized)return null;
        return data;
    }
}
