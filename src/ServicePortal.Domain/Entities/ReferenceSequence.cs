using System.ComponentModel.DataAnnotations;

namespace ServicePortal.Domain.Entities;

public class ReferenceSequence
{
    [Key]
    public int Year { get; set; }

    public int LastNumber { get; set; }

    [ConcurrencyCheck]
    public byte[] RowVersion { get; set; } = Guid.NewGuid().ToByteArray();
}
