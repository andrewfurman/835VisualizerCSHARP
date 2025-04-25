// FILE: X12EdiNetParser.cs
// ----------------------------------------
// X12EdiNetParser.cs – compatible with indice.Edi ≥ 1.10

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using indice.Edi;    // <- contains EdiTextReader / EdiToken
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
        Console.WriteLine($"[{DateTime.UtcNow:u}] Starting ParseFromString..."); // <<< DEBUGGING
        var grammar = EdiGrammar.NewX12();
        List<Segment> segments;

        try
        {
            segments = ReadAllSegments(edi, grammar);
            Console.WriteLine($"[{DateTime.UtcNow:u}] Read {segments.Count} segments."); // <<< DEBUGGING
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.UtcNow:u}] ERROR in ReadAllSegments: {ex.Message}"); // <<< DEBUGGING
            Console.WriteLine($"[{DateTime.UtcNow:u}] ReadAllSegments Exception Details: {ex}"); // <<< DEBUGGING
            throw new InvalidOperationException("Failed to read EDI segments.", ex); // Re-throw wrapped exception
        }

        try
        {
            /* ---------- high-level mapping ---------- */

            // Payer (N1*PR)
            Console.WriteLine($"[{DateTime.UtcNow:u}] Searching for N1*PR segment..."); // <<< DEBUGGING
            var n1Payer = segments.FirstOrDefault(s => s.Name == "N1" && s.Values.Length > 0 && s.Values[0] == "PR");
            var payerName = n1Payer?.Values.ElementAtOrDefault(1) ?? "";
            var payerId = n1Payer?.Values.ElementAtOrDefault(3) ?? "";
            Console.WriteLine($"[{DateTime.UtcNow:u}] Found Payer: Name='{payerName}', ID='{payerId}'"); // <<< DEBUGGING

            // Claims (CLP … until next CLP)
            Console.WriteLine($"[{DateTime.UtcNow:u}] Processing CLP segments..."); // <<< DEBUGGING
            var claims = segments
                .Select((seg, idx) => (seg, idx))
                .Where(t => t.seg.Name == "CLP")
                .Select(t =>
                {
                    var clp = t.seg;
                    var after = segments.Skip(t.idx + 1);
                    Console.WriteLine($"[{DateTime.UtcNow:u}]   Processing CLP segment at index {t.idx}: {string.Join('*', clp.Values)}"); // <<< DEBUGGING

                    try
                    {
                        var claimNumber = clp.Values.ElementAtOrDefault(0) ?? "UNKNOWN_CLAIM";
                        var totalCharge = decimal.Parse(clp.Values.ElementAtOrDefault(2) ?? "0", CultureInfo.InvariantCulture);
                        var totalPaid = decimal.Parse(clp.Values.ElementAtOrDefault(3) ?? "0", CultureInfo.InvariantCulture);

                        Console.WriteLine($"[{DateTime.UtcNow:u}]     ClaimNumber={claimNumber}, Charge={totalCharge}, Paid={totalPaid}"); // <<< DEBUGGING

                        var services = after
                            .TakeWhile(s => s.Name != "CLP")
                            .Where(s => s.Name == "SVC")
                            .Select(svc =>
                            {
                                Console.WriteLine($"[{DateTime.UtcNow:u}]       Processing SVC segment: {string.Join('*', svc.Values)}"); // <<< DEBUGGING
                                try
                                {
                                    var procComposite = svc.Values.ElementAtOrDefault(0) ?? ":"; // Handle missing composite
                                    var proc = procComposite.Contains(':') ? procComposite.Split(':').Last() : procComposite;
                                    var chg = decimal.Parse(svc.Values.ElementAtOrDefault(1) ?? "0", CultureInfo.InvariantCulture);
                                    var pd = decimal.Parse(svc.Values.ElementAtOrDefault(2) ?? "0", CultureInfo.InvariantCulture);
                                    Console.WriteLine($"[{DateTime.UtcNow:u}]         Service: Proc={proc}, Charge={chg}, Paid={pd}"); // <<< DEBUGGING
                                    return new ServiceLine(proc, chg, pd);
                                }
                                catch (FormatException fmtEx)
                                {
                                    Console.WriteLine($"[{DateTime.UtcNow:u}] ERROR parsing decimal in SVC segment: {fmtEx.Message}. Segment: {string.Join('*', svc.Values)}"); // <<< DEBUGGING
                                    throw new FormatException($"Error parsing decimal in SVC segment for claim {claimNumber}. Raw SVC: {string.Join('*', svc.Values)}", fmtEx);
                                }
                                catch (Exception svcEx)
                                {
                                     Console.WriteLine($"[{DateTime.UtcNow:u}] ERROR processing SVC segment: {svcEx.Message}. Segment: {string.Join('*', svc.Values)}"); // <<< DEBUGGING
                                    throw new InvalidOperationException($"Error processing SVC segment for claim {claimNumber}. Raw SVC: {string.Join('*', svc.Values)}", svcEx);
                                }
                            })
                            .ToList();

                        Console.WriteLine($"[{DateTime.UtcNow:u}]     Found {services.Count} service lines for claim {claimNumber}."); // <<< DEBUGGING
                        return new Claim(claimNumber, totalCharge, totalPaid, services);
                    }
                    catch (FormatException fmtEx)
                    {
                        Console.WriteLine($"[{DateTime.UtcNow:u}] ERROR parsing decimal in CLP segment: {fmtEx.Message}. Segment: {string.Join('*', clp.Values)}"); // <<< DEBUGGING
                        throw new FormatException($"Error parsing decimal in CLP segment. Raw CLP: {string.Join('*', clp.Values)}", fmtEx);
                    }
                    catch (Exception clpEx)
                    {
                         Console.WriteLine($"[{DateTime.UtcNow:u}] ERROR processing CLP segment: {clpEx.Message}. Segment: {string.Join('*', clp.Values)}"); // <<< DEBUGGING
                        throw new InvalidOperationException($"Error processing CLP segment. Raw CLP: {string.Join('*', clp.Values)}", clpEx);
                    }
                })
                .ToList();

            var grandTotal = claims.Sum(c => c.TotalPaid);
            Console.WriteLine($"[{DateTime.UtcNow:u}] Calculated Grand Total Paid: {grandTotal}"); // <<< DEBUGGING

            return new CanonicalRemit(
                FileName:       "x12-835.edi", // Consider making dynamic if needed
                PayerName:      payerName,
                PayerId:        payerId,
                GrandTotalPaid: grandTotal,
                Claims:         claims
            );
        }
        catch (Exception ex)
        {
             Console.WriteLine($"[{DateTime.UtcNow:u}] ERROR during high-level mapping: {ex.Message}"); // <<< DEBUGGING
             Console.WriteLine($"[{DateTime.UtcNow:u}] Mapping Exception Details: {ex}"); // <<< DEBUGGING
             throw; // Re-throw to be caught by the main handler
        }
    }

    /* ---------- helper ---------- */

    // X12EdiNetParser.cs   (only the read-loop is shown)

    private static List<Segment> ReadAllSegments(string edi, IEdiGrammar grammar)
    {
        Console.WriteLine($"[{DateTime.UtcNow:u}]   Entering ReadAllSegments..."); // <<< DEBUGGING
        var segments      = new List<Segment>();
        string? segName   = null;
        var     segValues = new List<string>();

        using var reader = new EdiTextReader(new StringReader(edi), grammar);

        while (reader.Read())
        {
            // Optional: Add very verbose logging here if needed
            // Console.WriteLine($"[{DateTime.UtcNow:u}]     Reader Token: {reader.TokenType}, Value: {reader.Value}"); // <<< VERBOSE DEBUGGING

            switch (reader.TokenType)
            {
                /* ——— NEW TOKENS ——— */
                case EdiToken.SegmentStart:       // ‘~’ just read → previous segment complete
                    Flush();
                    break;

                case EdiToken.SegmentName:        // “ISA”, “GS”, “CLP”, …
                    segName = reader.Value!.ToString();
                    break;

                case EdiToken.String:             // every element / component value
                    segValues.Add(reader.Value?.ToString() ?? string.Empty);
                    break;
            }
        }

        Flush();                                  // flush last segment
        Console.WriteLine($"[{DateTime.UtcNow:u}]   Exiting ReadAllSegments. Found {segments.Count} segments."); // <<< DEBUGGING
        return segments;

        void Flush()
        {
            if (segName != null)
            {
                // Console.WriteLine($"[{DateTime.UtcNow:u}]     Flushing Segment: {segName}"); // <<< VERBOSE DEBUGGING
                segments.Add(new Segment(segName, segValues.ToArray()));
                segName   = null;
                segValues = new List<string>();
            }
        }
    }
}