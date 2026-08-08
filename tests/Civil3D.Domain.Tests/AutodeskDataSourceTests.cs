using Civil3D.Domain.Alignments.Data;
using Civil3D.Domain.Errors;
using Xunit;
using static Civil3D.Domain.Tests.TestDoubles;

namespace Civil3D.Domain.Tests;

/// <summary>
/// Exercises the real <c>Autodesk*DataSource</c> wiring against the document-context seam
/// (without Civil 3D): the data source correctly surfaces the context's error mapping.
/// </summary>
public class AutodeskDataSourceTests
{
    [Fact]
    public void ReadAll_WithoutActiveDocument_ThrowsNoActiveDocument()
    {
        var dataSource = new AutodeskAlignmentDataSource(new FakeDocumentContext(hasDocument: false));

        DomainException ex = Assert.Throws<DomainException>(() => dataSource.ReadAll());

        Assert.Equal(DomainErrorCode.NoActiveDocument, ex.Code);
    }

    [Fact]
    public void ReadAll_FailedContextRead_MapsToTransactionFailed()
    {
        // A non-Database payload fails the cast inside the real data source; the context seam
        // (mimicked by the fake) maps that Autodesk-side failure to TransactionFailed.
        var dataSource = new AutodeskAlignmentDataSource(
            new FakeDocumentContext(hasDocument: true, database: new object()));

        DomainException ex = Assert.Throws<DomainException>(() => dataSource.ReadAll());

        Assert.Equal(DomainErrorCode.TransactionFailed, ex.Code);
    }
}
