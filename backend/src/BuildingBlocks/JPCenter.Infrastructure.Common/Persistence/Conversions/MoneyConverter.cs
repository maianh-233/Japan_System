using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JPCenter.Infrastructure.Common.Persistence.Conversions;

/// <summary><see cref="Money"/> ↔ cột <c>numeric(18,0)</c>.</summary>
public sealed class MoneyConverter() : ValueConverter<Money, decimal>(m => m.Amount, d => Money.FromTrusted(d));
