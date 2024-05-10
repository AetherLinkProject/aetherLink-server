using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

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
    }
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