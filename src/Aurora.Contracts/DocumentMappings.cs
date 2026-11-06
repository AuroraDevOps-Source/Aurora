namespace Aurora.Contracts;

public sealed record MappingCandidate(string Label, string Value, int? Page = null, string? AccountId = null);
public sealed class DocumentMappingQuestion
{
    public string Field { get; set; } = "";
    public string Reason { get; set; } = "";
    public MappingCandidate[] Candidates { get; set; } = [];
}
public sealed record DocumentMappingRule(string Sender, string Form, string Field, string Label);

public static class DocumentMappings
{
    public static void PrepareBilling(OrderDocumentDraft draft,string rules)
    {
        // Only apply the seeded billing policy while those instructions are enabled.
        if (!rules.Contains("Prepaid: the shipper is the Bill To. Collect: the consignee is the Bill To.",StringComparison.Ordinal)
            || draft.MappingQuestions.Any(q=>q.Field=="BillTo")) return;
        var party=draft.FreightTerms switch { "Prepaid"=>"pickup", "Collect"=>"delivery", _=>null };
        if(party is null && draft.FreightTerms!="Third Party")return;
        string? reason=null;
        if(party is not null)
        {
            var name=party=="pickup"?draft.Order.Shipper:draft.Order.Customer;
            if(string.IsNullOrWhiteSpace(name))reason="The freight terms identify a payer whose name is missing.";
            else if(!string.IsNullOrWhiteSpace(draft.Order.BillTo) && !string.Equals(draft.Order.BillTo.Trim(),name.Trim(),StringComparison.OrdinalIgnoreCase))
                reason="The printed payer conflicts with the freight terms.";
            else if (!string.IsNullOrWhiteSpace(draft.BillToAccountId) && !string.IsNullOrWhiteSpace(draft.AccountId(party))
                && !string.Equals(draft.BillToAccountId.Trim(),draft.AccountId(party).Trim(),StringComparison.OrdinalIgnoreCase))
                reason="The printed payer account conflicts with the freight terms.";
            else SetBillTo(draft,party,fillMissingOnly:true);
        }
        else if(string.IsNullOrWhiteSpace(draft.Order.BillTo) && string.IsNullOrWhiteSpace(draft.BillToAccountId))
            reason="Third Party is marked, but the payer is missing.";
        if(reason is not null)draft.MappingQuestions=[..draft.MappingQuestions,new(){Field="BillTo",Reason=reason}];
    }

    public static void SetBillTo(OrderDocumentDraft draft, string party, bool fillMissingOnly = false)
    {
        var source = NovaImportAddress.Parties.Single(p=>p.Part==party);
        var target = NovaImportAddress.Parties.Single(p=>p.Part=="header");
        for (var i=0;i<target.Properties.Length;i++)
        {
            var value=typeof(TmsOrder).GetProperty(source.Properties[i])!.GetValue(draft.Order);
            var property=typeof(TmsOrder).GetProperty(target.Properties[i])!;
            if (!fillMissingOnly || string.IsNullOrWhiteSpace(property.GetValue(draft.Order) as string)) property.SetValue(draft.Order,value);
        }
        if (!fillMissingOnly || string.IsNullOrWhiteSpace(draft.BillToAccountId)) draft.BillToAccountId=draft.AccountId(party);
    }

    public static void Answer(OrderDocumentDraft draft, DocumentMappingQuestion question, MappingCandidate choice)
    {
        if (question.Field=="PRO")
        {
            var (pro,terminal)=OrderDocumentDraft.SplitTag(choice.Value);
            draft.TagNumber=choice.Value.Trim(); draft.Order.Id=pro;
            if (terminal.Length>0) draft.Order.TerminalCode=terminal;
        }
        else if (question.Field=="BillTo")
        {
            var matchesPickup=string.Equals(draft.Order.Shipper?.Trim(),choice.Value.Trim(),StringComparison.OrdinalIgnoreCase);
            var matchesDelivery=string.Equals(draft.Order.Customer?.Trim(),choice.Value.Trim(),StringComparison.OrdinalIgnoreCase);
            var party=matchesPickup==matchesDelivery ? null : matchesPickup ? "pickup" : "delivery";
            // A changed payer must never inherit a different payer's billing address.
            foreach (var property in NovaImportAddress.Parties.Single(p=>p.Part=="header").Properties)
                typeof(TmsOrder).GetProperty(property)!.SetValue(draft.Order,"");
            draft.Order.BillTo=choice.Value.Trim();
            draft.BillToAccountId=choice.AccountId?.Trim()??"";
            if(party is not null) SetBillTo(draft,party);
            if(!string.IsNullOrWhiteSpace(choice.AccountId)) draft.BillToAccountId=choice.AccountId.Trim();
            draft.PrintedBillToAccountLine=null;
            draft.EditedAddressParts.Remove("header");
        }
        draft.MappingQuestions=draft.MappingQuestions.Where(q=>q!=question).ToArray();
    }
}
