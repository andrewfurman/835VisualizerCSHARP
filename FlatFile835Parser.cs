// FlatFile835Parser.cs

// Reads the file line-by-line with StreamReader.

// Switches on the record code (H, P, C, S) to fill the CanonicalRemit object.

// For the demo you can hard-code the element positions you actually need (e.g. in an S* line, element 8 is Paid Amount).

// Exposes a single method:

// csharp
// Copy
// Edit
// public static CanonicalRemit Parse(string path);
// Throws a clear error if the first non-empty line doesn’t begin with "H*" (so you know you fed it the wrong format).

using Models;

public static class FlatFile835Parser
{
    public static CanonicalRemit ParseFromString(string edi)
    {
        var lines = edi.Split(
            new[] { "\r\n", "\n" },
            StringSplitOptions.RemoveEmptyEntries);

        if (!lines[0].StartsWith("H*"))
            throw new InvalidDataException("Not an Amisys flat-file 835");

        var fileName   = lines[0].Split('*')[1];
        var payerName  = "";
        var claims     = new List<Claim>();
        Claim? current = null;

        foreach (var line in lines)
        {
            var parts = line.Split('*');
            switch (parts[0])
            {
                case "P":           // claim header
                    if (current != null) claims.Add(current);
                    current = new Claim(
                        ClaimNumber: parts[2],      // HPI
                        TotalCharge: 0m,
                        TotalPaid:   0m,
                        Lines: new List<ServiceLine>());
                    break;

                case "C":           // claim detail – you can ignore or map more fields
                    payerName = parts[6];           // MGN field
                    break;

                case "S":           // service line
                    if (current == null) break;
                    var svc = new ServiceLine(
                        ProcedureCode: parts[4],    // DX1
                        Charge:        decimal.Parse(parts.Last()),
                        Paid:          decimal.Parse(parts[9])); // MDP or SR1
                    current.Lines.Add(svc);
                    current.TotalPaid += svc.Paid;
                    current.TotalCharge += svc.Charge;
                    break;
            }
        }

        if (current != null) claims.Add(current);

        return new CanonicalRemit(
            FileName: fileName,
            PayerName: payerName,
            PayerId:   "",            // fill later
            GrandTotalPaid: claims.Sum(c => c.TotalPaid),
            Claims: claims);
    }
}
