using E.DataLinq.Core.IO;

namespace E.DataLinq.Test.DataLinqCore.UnitTests;

[TestClass]
public class FileExTests
{
    private string _testFilePath;

    [TestInitialize]
    public void Setup()
    {
        _testFilePath = Path.GetTempFileName();
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    #region ReadBottomLinesAsync Tests

    [TestMethod]
    public async Task ReadBottomLinesAsync_WithFewerLinesThanRequested_ReturnsAllLinesReversed()
    {
        // Arrange
        string[] testContent = { "Line 1", "Line 2", "Line 3" };
        File.WriteAllLines(_testFilePath, testContent);

        // Act
        var result = await FileEx.ReadBottomLinesAsync(_testFilePath, 5);

        // Assert
        Assert.AreEqual(3, result.Length);
        Assert.AreEqual("Line 3", result[0]);
        Assert.AreEqual("Line 2", result[1]);
        Assert.AreEqual("Line 1", result[2]);
    }

    [TestMethod]
    public async Task ReadBottomLinesAsync_WithMoreLinesThanRequested_ReturnsLastNLinesReversed()
    {
        // Arrange
        string[] testContent = { "Line 1", "Line 2", "Line 3", "Line 4", "Line 5" };
        File.WriteAllLines(_testFilePath, testContent);

        // Act
        var result = await FileEx.ReadBottomLinesAsync(_testFilePath, 3);

        // Assert
        Assert.AreEqual(3, result.Length);
        Assert.AreEqual("Line 5", result[0]);
        Assert.AreEqual("Line 4", result[1]);
        Assert.AreEqual("Line 3", result[2]);
    }

    [TestMethod]
    public async Task ReadBottomLinesAsync_WithFilter_ReturnsOnlyMatchingLines()
    {
        // Arrange
        string[] testContent =
        {
            "Apple Line",
            "Banana Line",
            "Apple Fruit",
            "Orange Line",
            "Apple Orange",
        };
        File.WriteAllLines(_testFilePath, testContent);

        // Act
        var result = await FileEx.ReadBottomLinesAsync(_testFilePath, 10, "Apple");

        // Assert
        Assert.AreEqual(3, result.Length);
        Assert.AreEqual("Apple Orange", result[0]);
        Assert.AreEqual("Apple Fruit", result[1]);
        Assert.AreEqual("Apple Line", result[2]);
    }

    [TestMethod]
    public async Task ReadBottomLinesAsync_WithFilterAndLimit_ReturnsLastMatchingLines()
    {
        // Arrange
        string[] testContent =
        {
            "Apple 1",
            "Banana 1",
            "Apple 2",
            "Orange 1",
            "Apple 3",
            "Grape 1",
            "Apple 4",
        };
        File.WriteAllLines(_testFilePath, testContent);

        // Act
        var result = await FileEx.ReadBottomLinesAsync(_testFilePath, 2, "Apple");

        // Assert
        Assert.AreEqual(2, result.Length);
        Assert.AreEqual("Apple 4", result[0]);
        Assert.AreEqual("Apple 3", result[1]);
    }

    [TestMethod]
    public async Task ReadBottomLinesAsync_WithEmptyLines_SkipsEmptyLines()
    {
        // Arrange
        string[] testContent = { "Line 1", "", "   ", "Line 2", "" };
        File.WriteAllLines(_testFilePath, testContent);

        // Act
        var result = await FileEx.ReadBottomLinesAsync(_testFilePath, 5);

        // Assert
        Assert.AreEqual(2, result.Length);
        Assert.AreEqual("Line 2", result[0]);
        Assert.AreEqual("Line 1", result[1]);
    }

    [TestMethod]
    public async Task ReadBottomLinesAsync_WithEmptyFile_ReturnsEmptyArray()
    {
        // Arrange
        File.WriteAllText(_testFilePath, string.Empty);

        // Act
        var result = await FileEx.ReadBottomLinesAsync(_testFilePath, 5);

        // Assert
        Assert.AreEqual(0, result.Length);
    }

    #endregion

    #region ReadTopLinesAsync Tests

    [TestMethod]
    public async Task ReadTopLinesAsync_WithFewerLinesThanRequested_ReturnsAllLinesReversed()
    {
        // Arrange
        string[] testContent = { "Line 1", "Line 2", "Line 3" };
        File.WriteAllLines(_testFilePath, testContent);

        // Act
        var result = await FileEx.ReadTopLinesAsync(_testFilePath, 5);

        // Assert
        Assert.AreEqual(3, result.Length);
        Assert.AreEqual("Line 1", result[0]);
        Assert.AreEqual("Line 2", result[1]);
        Assert.AreEqual("Line 3", result[2]);
    }

    [TestMethod]
    public async Task ReadTopLinesAsync_WithMoreLinesThanRequested_ReturnsFirstNLines()
    {
        // Arrange
        string[] testContent = { "Line 1", "Line 2", "Line 3", "Line 4", "Line 5" };
        File.WriteAllLines(_testFilePath, testContent);

        // Act
        var result = await FileEx.ReadTopLinesAsync(_testFilePath, 3);

        // Assert
        Assert.AreEqual(3, result.Length);
        Assert.AreEqual("Line 1", result[0]);
        Assert.AreEqual("Line 2", result[1]);
        Assert.AreEqual("Line 3", result[2]);
    }

    [TestMethod]
    public async Task ReadTopLinesAsync_WithFilter_ReturnsOnlyMatchingLines()
    {
        // Arrange
        string[] testContent =
        {
            "Apple Line",
            "Banana Line",
            "Apple Fruit",
            "Orange Line",
            "Apple Orange",
        };
        File.WriteAllLines(_testFilePath, testContent);

        // Act
        var result = await FileEx.ReadTopLinesAsync(_testFilePath, 10, "Apple");

        // Assert
        Assert.AreEqual(3, result.Length);
        Assert.AreEqual("Apple Line", result[0]);
        Assert.AreEqual("Apple Fruit", result[1]);
        Assert.AreEqual("Apple Orange", result[2]);
    }

    [TestMethod]
    public async Task ReadTopLinesAsync_WithFilterAndLimit_ReturnsFirstMatchingLines()
    {
        // Arrange
        string[] testContent =
        {
            "Apple 1",
            "Banana 1",
            "Apple 2",
            "Orange 1",
            "Apple 3",
            "Grape 1",
            "Apple 4",
        };
        File.WriteAllLines(_testFilePath, testContent);

        // Act
        var result = await FileEx.ReadTopLinesAsync(_testFilePath, 2, "Apple");

        // Assert
        Assert.AreEqual(2, result.Length);
        Assert.AreEqual("Apple 1", result[0]);
        Assert.AreEqual("Apple 2", result[1]);
    }

    [TestMethod]
    public async Task ReadTopLinesAsync_WithEmptyLines_SkipsEmptyLines()
    {
        // Arrange
        string[] testContent = { "Line 1", "", "   ", "Line 2", "" };
        File.WriteAllLines(_testFilePath, testContent);

        // Act
        var result = await FileEx.ReadTopLinesAsync(_testFilePath, 5);

        // Assert
        Assert.AreEqual(2, result.Length);
        Assert.AreEqual("Line 1", result[0]);
        Assert.AreEqual("Line 2", result[1]);
    }

    [TestMethod]
    public async Task ReadTopLinesAsync_WithEmptyFile_ReturnsEmptyArray()
    {
        // Arrange
        File.WriteAllText(_testFilePath, string.Empty);

        // Act
        var result = await FileEx.ReadTopLinesAsync(_testFilePath, 5);

        // Assert
        Assert.AreEqual(0, result.Length);
    }

    #endregion
}
