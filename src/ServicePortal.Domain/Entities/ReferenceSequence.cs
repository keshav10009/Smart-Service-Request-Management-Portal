using System.ComponentModel.DataAnnotations;

namespace ServicePortal.Domain.Entities;

public class ReferenceSequence
{
    [Key]
    public int Year { get; set; }

    public int LastNumber { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
}
