namespace Aurora.Contracts;

public sealed record MappingCandidate(string Label, string Value, int? Page = null, string? AccountId = null);
public sealed class DocumentMappingQuestion
{
    public string Field { get; set; } = "";
    public string Reason { get; set; } = "";
    public MappingCandidate[] Candidates { get; set; } = [];
}
public sealed record DocumentMappingRule(string Sender, string Form, string Field, string Label, string Account="");

public static class DocumentMappings
{
    public static void ApplyToMatchingOrder(OrderDocumentDraft draft, DocumentMappingRule rule)
    {
        static bool Same(string a,string b)=>string.Equals(a?.Trim(),b?.Trim(),StringComparison.OrdinalIgnoreCase);
        if(!Same(draft.MappingSender,rule.Sender) || !Same(draft.MappingForm,rule.Form) || !Same(draft.MappingAccount,rule.Account))return;
        var question=draft.MappingQuestions.FirstOrDefault(q=>q.Field==rule.Field);
        if(question?.Candidates.Where(c=>Same(c.Label,rule.Label)).Take(2).ToArray() is [var candidate])
        {
            try { Answer(draft,question,candidate); } catch(FormatException) { /* Leave the question open for this order. */ }
        }
    }

    public static void RequireBol(OrderDocumentDraft draft, bool reopen=false, bool extracted=false)
    {
        var question=draft.MappingQuestions.FirstOrDefault(q=>q.Field=="BOL");
        if(question is null)
        {
            if(!reopen && !extracted && !string.IsNullOrWhiteSpace(draft.BillOfLading))return;
            question=new(){Field="BOL",Reason="Which printed field contains the bill of lading number?"};
            draft.MappingQuestions=[..draft.MappingQuestions,question];
        }

        var choices=new List<MappingCandidate>();
        foreach(var candidate in (question.Candidates??[]).Concat(draft.PrintedIdentifiers??[]))
        {
            if(candidate is null || string.IsNullOrWhiteSpace(candidate.Label) || string.IsNullOrWhiteSpace(candidate.Value))continue;
            if(!choices.Any(c=>Same(c.Label,candidate.Label) && Same(c.Value,candidate.Value)))
                choices.Add(candidate with {Label=candidate.Label.Trim(),Value=candidate.Value.Trim()});
        }

        void Offer(string label,string? value)
        {
            if(!string.IsNullOrWhiteSpace(value) && !choices.Any(c=>Same(c.Value,value)))
                choices.Add(new(label,value.Trim()));
        }
        if (!reopen)
        {
            var recognized = choices.Where(c => System.Text.RegularExpressions.Regex.IsMatch(c.Label,
                @"^(?:HAWB|BOL|Bill\s+of\s+Lading)\s*(?:#|No\.?|Num\.?|Number)?\s*:?$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant))
                .Select(c => c.Value).ToList();
            if (!string.IsNullOrWhiteSpace(draft.HouseAirWaybill)) recognized.Add(draft.HouseAirWaybill.Trim());
            if (!string.IsNullOrWhiteSpace(draft.BillOfLading)) recognized.Add(draft.BillOfLading.Trim());
            var distinct = recognized.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (distinct.Length == 1)
            {
                Answer(draft, question, new("BOL", distinct[0]));
                return;
            }
            if (distinct.Length > 1)
            {
                if (!string.IsNullOrWhiteSpace(draft.BillOfLading) && !choices.Any(c => Same(c.Label,"BOL") && Same(c.Value,draft.BillOfLading)))
                    choices.Insert(0,new("BOL",draft.BillOfLading.Trim()));
                draft.Order.Reference = draft.BillOfLading = "";
            }
        }
        Offer("HAWB #",draft.HouseAirWaybill);
        Offer("MAWB #",draft.MasterAirWaybill);
        Offer("Shipment reference",draft.ShipmentReference);
        Offer("P.O. #",draft.PurchaseOrder);
        Offer("Tag #",draft.PrintedLykesTag);
        question.Candidates=choices.Take(12).ToArray();
        static bool Same(string a,string b)=>string.Equals(a.Trim(),b.Trim(),StringComparison.OrdinalIgnoreCase);
    }

    public static void PrepareBilling(OrderDocumentDraft draft,string rules)
    {
        var lykes=string.Equals(draft.DocumentType,"Lykes",StringComparison.OrdinalIgnoreCase);
        if (lykes && draft.ApplyPrintedAccount())
        {
            draft.LogoCompany=null;
            draft.BillingEvidence="Bill To from the printed Account header: "+draft.PrintedBillToAccountLine;
            draft.MappingQuestions=draft.MappingQuestions.Where(q=>q.Field!="BillTo").ToArray();
            return;
        }
        if (lykes)
        {
            draft.LogoCompany=null;
            if (!draft.MappingQuestions.Any(q=>q.Field=="BillTo"))
                draft.MappingQuestions=[..draft.MappingQuestions,new(){Field="BillTo",Reason="The Lykes Account header could not be verified."}];
            return;
        }
        if (!string.IsNullOrWhiteSpace(draft.IndexedBillToPayer))
        {
            draft.LogoCompany=null;
            if (!string.Equals(draft.Order.BillTo?.Trim(),draft.IndexedBillToPayer.Trim(),StringComparison.OrdinalIgnoreCase))
            {
                var question=draft.MappingQuestions.FirstOrDefault(q=>q.Field=="BillTo");
                if (question is null)
                {
                    question=new(){Field="BillTo"};
                    draft.MappingQuestions=[..draft.MappingQuestions,question];
                }
                question.Reason="The printed payer and extracted billing details disagree. Confirm the payer.";
                var printed=new MappingCandidate("Printed payer",draft.IndexedBillToPayer,draft.SourcePages.FirstOrDefault());
                question.Candidates=[printed,..question.Candidates.Where(c=>!string.Equals(c.Value,printed.Value,StringComparison.OrdinalIgnoreCase))];
            }
            else draft.BillingEvidence="Bill To from the printed payer block: "+draft.Order.BillTo;
            return;
        }
        if (!string.IsNullOrWhiteSpace(draft.PrintedBillToPayer) &&
            string.Equals(draft.Order.BillTo?.Trim(),draft.PrintedBillToPayer.Trim(),StringComparison.OrdinalIgnoreCase))
        {
            draft.LogoCompany=null;
            draft.BillingEvidence="Bill To from the printed Bill To block: "+draft.Order.BillTo;
            return;
        }
        if (draft.FreightTerms=="Third Party" && !string.IsNullOrWhiteSpace(draft.PrintedThirdPartyPayer) &&
            string.Equals(draft.Order.BillTo?.Trim(),draft.PrintedThirdPartyPayer.Trim(),StringComparison.OrdinalIgnoreCase))
        {
            draft.LogoCompany=null;
            draft.BillingEvidence="Bill To from the marked third-party billing block: "+draft.Order.BillTo;
            return;
        }
        if(draft.LogoCompany is {Page:>0} logo && !string.IsNullOrWhiteSpace(logo.Name) &&
            !System.Text.RegularExpressions.Regex.IsMatch(logo.Name,@"\bU[\s-]*LINE\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            var o=draft.Order;
            o.BillTo=logo.Name.Trim();
            o.BillToAddress=logo.Address?.Trim()??"";
            o.BillToAddress2=logo.Address2?.Trim()??"";
            o.BillToCity=logo.City?.Trim()??"";
            o.BillToState=logo.State?.Trim()??"";
            o.BillToPostalCode=logo.PostalCode?.Trim()??"";
            o.BillToCountry=logo.Country?.Trim()??"";
            draft.BillToContact=logo.Contact?.Trim();
            draft.BillToPhone=logo.Phone?.Trim();
            draft.BillToEmail=logo.Email?.Trim();
            draft.BillToAccountId="";
            draft.PrintedBillToAccountLine=null;
            draft.SeparatePartyAddresses();
            draft.BillingEvidence=$"Bill To from company logo: {o.BillTo} (page {logo.Page}).";
            draft.MappingQuestions=draft.MappingQuestions.Where(q=>q.Field!="BillTo").ToArray();
            return;
        }
        draft.LogoCompany=null;
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
        else if (question.Field=="BOL")
        {
            var bol=choice.Value.Trim();
            if (bol.Length is 0 or >200) throw new FormatException("Enter a BOL number of up to 200 characters.");
            draft.BillOfLading=bol;
            draft.Order.Reference=bol;
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
            draft.SeparatePartyAddresses();
            draft.PrintedBillToAccountLine=null;
            draft.EditedAddressParts.Remove("header");
        }
        draft.MappingQuestions=draft.MappingQuestions.Where(q=>q!=question).ToArray();
    }
}
