namespace FtrIO.OneTwo;

internal interface IFlagSource
{
    Task<IReadOnlyList<ImportedFlag>> FetchAsync(CancellationToken cancellationToken = default);
}
