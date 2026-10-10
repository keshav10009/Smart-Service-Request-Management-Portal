using Microsoft.EntityFrameworkCore;
using ServicePortal.Application.Interfaces;
using ServicePortal.Domain.Entities;
using ServicePortal.Infrastructure.Data;

namespace ServicePortal.Infrastructure.Services;

public class ReferenceNumberGenerator : IReferenceNumberGenerator
{
    private readonly ApplicationDbContext _context;
    private const int MaxRetries = 5;

    public ReferenceNumberGenerator(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateAsync(CancellationToken cancellationToken = default)
    {
        var currentYear = DateTime.UtcNow.Year;

        for (var retry = 0; retry < MaxRetries; retry++)
        {
            try
            {
                var sequence = await _context.ReferenceSequences
                    .FirstOrDefaultAsync(s => s.Year == currentYear, cancellationToken);

                if (sequence == null)
                {
                    sequence = new ReferenceSequence
                    {
                        Year = currentYear,
                        LastNumber = 1
                    };
                    _context.ReferenceSequences.Add(sequence);
                }
                else
                {
                    sequence.LastNumber++;
                }

                await _context.SaveChangesAsync(cancellationToken);

                return $"SR-{currentYear}-{sequence.LastNumber:D6}";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (retry == MaxRetries - 1)
                {
                    throw new InvalidOperationException("Could not generate a unique reference number due to high concurrency. Please try again.");
                }

                // Detach tracked entries and wait briefly before retrying
                _context.ChangeTracker.Clear();
                await Task.Delay(50 * (retry + 1), cancellationToken);
            }
        }

        throw new InvalidOperationException("Reference number generation failed.");
    }
}
