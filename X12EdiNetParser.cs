
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using indice.Edi;
using indice.Edi.Serialization;
using Models;

public static class X12EdiNetParser
{
    public static CanonicalRemit ParseFromString(string edi)
    {
        var grammar = EdiGrammar.NewX12();
        var segments = new List<EdiSegment>();
        
        using (var reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(edi))))
        {
            var parser = new EdiParser(reader, grammar);
            while (parser.Read())
            {
                if (parser.IsSegment)
                {
                    segments.Add(parser.Segment);
                }
            }
        }
        
        var payerName = segments
            .Where(s => s.Name == "N1" && s.Values[1].Contains("INSURANCE"))
            .Select(s => s.Values[1])
            .FirstOrDefault() ?? "";
            
        var claims = segments
            .Where(s => s.Name == "CLP")
            .Select(clp => {
                var claimNumber = clp.Values[1];
                var totalCharge = decimal.Parse(clp.Values[3]);
                var totalPaid = decimal.Parse(clp.Values[4]);
                
                var services = segments
                    .SkipWhile(s => s != clp)
                    .TakeWhile(s => s.Name != "CLP")
                    .Where(s => s.Name == "SVC")
                    .Select(svc => new ServiceLine(
                        ProcedureCode: svc.Values[1].Split(':')[1],
                        Charge: decimal.Parse(svc.Values[2]),
                        Paid: decimal.Parse(svc.Values[3])
                    ))
                    .ToList();
                    
                return new Claim(
                    ClaimNumber: claimNumber,
                    TotalCharge: totalCharge, 
                    TotalPaid: totalPaid,
                    Lines: services
                );
            })
            .ToList();
            
        return new CanonicalRemit(
            FileName: "x12-835.edi",
            PayerName: payerName,
            PayerId: segments.First(s => s.Name == "N1" && s.Values[1].Contains("INSURANCE")).Values[3],
            GrandTotalPaid: claims.Sum(c => c.TotalPaid),
            Claims: claims
        );
    }
}
