using System.ComponentModel.DataAnnotations;

namespace ServicePortal.Domain.Entities;

public class ReferenceSequence
{
    public int Id { get; set; }

    public int Year { get; set; }

    public int LastNumber { get; set; }

    [ConcurrencyCheck]
    public byte[] RowVersion { get; set; } = Guid.NewGuid().ToByteArray();
}
