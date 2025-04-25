// X12EdiNetParser.cs – compatible with indice.Edi ≥ 1.10

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using indice.Edi;          // <- contains EdiTextReader / EdiToken
using Models;

#nullable enable
public static class X12EdiNetParser
{
    // Convenience record to hold a parsed segment.
    private record Segment(string Name, string[] Values);

    /// <summary>Parses an X12-835 interchange held in <paramref name="edi"/> and
    /// converts it to the CanonicalRemit model used by the demo UI.</summary>
    public static CanonicalRemit ParseFromString(string edi)
    {
        var grammar  = EdiGrammar.NewX12();
        var segments = ReadAllSegments(edi, grammar);

        /* ---------- high-level mapping ---------- */

        // Payer (N1*PR)
        var n1Payer = segments.FirstOrDefault(s => s.Name == "N1" && s.Values[0] == "PR");
        var payerName = n1Payer?.Values.ElementAtOrDefault(1) ?? "";
        var payerId   = n1Payer?.Values.ElementAtOrDefault(3) ?? "";

        // Claims (CLP … until next CLP)
        var claims = segments
            .Select((seg, idx) => (seg, idx))
            .Where(t => t.seg.Name == "CLP")
            .Select(t =>
            {
                var clp   = t.seg;
                var after = segments.Skip(t.idx + 1);

                var claimNumber = clp.Values[0];
                var totalCharge = decimal.Parse(clp.Values[2], CultureInfo.InvariantCulture);
                var totalPaid   = decimal.Parse(clp.Values[3], CultureInfo.InvariantCulture);

                var services = after
                    .TakeWhile(s => s.Name != "CLP")
                    .Where(s => s.Name == "SVC")
                    .Select(svc =>
                    {
                        var proc = svc.Values[0].Split(':').Last();          // HC:99213 → 99213
                        var chg  = decimal.Parse(svc.Values[1], CultureInfo.InvariantCulture);
                        var pd   = decimal.Parse(svc.Values[2], CultureInfo.InvariantCulture);
                        return new ServiceLine(proc, chg, pd);
                    })
                    .ToList();

                return new Claim(claimNumber, totalCharge, totalPaid, services);
            })
            .ToList();

        return new CanonicalRemit(
            FileName:        "x12-835.edi",
            PayerName:       payerName,
            PayerId:         payerId,
            GrandTotalPaid:  claims.Sum(c => c.TotalPaid),
            Claims:          claims
        );
    }

    /* ---------- helper ---------- */

    // X12EdiNetParser.cs   (only the read-loop is shown)

    private static List<Segment> ReadAllSegments(string edi, IEdiGrammar grammar)
    {
        var segments      = new List<Segment>();
        string? segName   = null;
        var     segValues = new List<string>();

        using var reader = new EdiTextReader(new StringReader(edi), grammar);

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                /* ——— NEW TOKENS ——— */
                case EdiToken.SegmentStart:          // ‘~’ just read → previous segment complete
                    Flush();
                    break;

                case EdiToken.SegmentName:           // “ISA”, “GS”, “CLP”, …
                    segName = reader.Value!.ToString();
                    break;

                case EdiToken.String:                // every element / component value
                    segValues.Add(reader.Value?.ToString() ?? string.Empty);
                    break;
            }
        }

        Flush();                                    // flush last segment
        return segments;

        void Flush()
        {
            if (segName != null)
            {
                segments.Add(new Segment(segName, segValues.ToArray()));
                segName   = null;
                segValues = new List<string>();
            }
        }
    }
}