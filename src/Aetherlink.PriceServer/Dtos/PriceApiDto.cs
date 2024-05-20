using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Aetherlink.PriceServer.Common;

namespace Aetherlink.PriceServer.Dtos;

public class GetTokenPriceRequestDto : IValidatableObject
{
    [Required] public string TokenPair { get; set; }
    public SourceType Source { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(typeof(SourceType), Source))
        {
            yield return new ValidationResult("Invalid SourceType");
        }

        if (!TokenPairHelper.IsValidTokenPair(TokenPair))
        {
            yield return new ValidationResult("Invalid TokenPair");
        }
    }
}

public class GetTokenPriceListRequestDto : IValidatableObject
{
    [Required] public List<string> TokenPairs { get; set; }
    public SourceType Source { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(typeof(SourceType), Source))
        {
            yield return new ValidationResult("Invalid SourceType");
        }

        if (!TokenPairs.All(TokenPairHelper.IsValidTokenPair))
        {
            yield return new ValidationResult("Invalid TokenPairs input");
        }
    }
}

public class GetAggregatedTokenPriceRequestDto : IValidatableObject
{
    [Required] public string TokenPair { get; set; }
    public AggregateType AggregateType { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(typeof(AggregateType), AggregateType))
        {
            yield return new ValidationResult("Invalid AggregateType");
        }

        if (!TokenPairHelper.IsValidTokenPair(TokenPair))
        {
            yield return new ValidationResult("Invalid TokenPair");
        }
    }
}

public class GetPriceForLast24HoursRequestDto
{
    [Required] public string TokenPair { get; set; }
}

public class PriceForLast24HoursResponseDto
{
    public List<PriceDto> Prices { get; set; }
    public double ChangeRate24Hours { get; set; }
}

public class PriceListResponseDto
{
    public string Source { get; set; }
    public List<PriceDto> Prices { get; set; }
}

public class PriceResponseDto
{
    public string Source { get; set; }
    public PriceDto Data { get; set; }
}

public class AggregatedPriceResponseDto
{
    public string AggregateType { get; set; }
    public PriceDto Data { get; set; }
}