// Models/CanonicalRemit.cs

// Plain C# record with the handful of fields your demo UI will show (e.g. PayerName, TotalPaid, List<Claim>).

// Keeps business data separate from parsing logic so you can reuse it when you add the X12 parser later.

namespace Models;

public record ServiceLine(
    string ProcedureCode,
    decimal Charge,
    decimal Paid
);

public record Claim(
    string ClaimNumber,
    decimal TotalCharge,
    decimal TotalPaid,
    List<ServiceLine> Lines
);

public record CanonicalRemit(
    string FileName,
    string PayerName,
    string PayerId,
    decimal GrandTotalPaid,
    List<Claim> Claims
);
