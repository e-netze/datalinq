var builder = DistributedApplication.CreateBuilder(args);

var dataLinqCode = builder.AddProject<Projects.DataLinq_Code>("datalinq-code").WithHttpsEndpoint();
var datdaLinqApi = builder.AddProject<Projects.DataLinq_Api>("datalinq-api");

dataLinqCode.WithEnvironment(e =>
{
    // Add an environment variable to override the first ClientEndpoint in configuration
    // Dynamically assigned HTTPS URL
    e.EnvironmentVariables.Add("DataLinq.Code__Instances__0__Name", "DataLinq.CodeApi");
    e.EnvironmentVariables.Add("DataLinq.Code__Instances__0__Description", "DataLinq.CodeApi instance for testing and development");
    e.EnvironmentVariables.Add("DataLinq.Code__Instances__0__LoginUrl", $"{datdaLinqApi.GetEndpoint("https").Url}");
    e.EnvironmentVariables.Add("DataLinq.Code__Instances__0__LogoutUrl", $"{datdaLinqApi.GetEndpoint("https").Url}");
    e.EnvironmentVariables.Add("DataLinq.Code__Instances__0__CodeApiClientUrl", $"{datdaLinqApi.GetEndpoint("https").Url}");
});


datdaLinqApi.WithEnvironment(e =>
{
    // Add an environment variable to override the first ClientEndpoint in configuration
    // Dynamically assigned HTTPS URL
    e.EnvironmentVariables.Add("DataLinq__CodeApi__ClientEndpoints__0", dataLinqCode.GetEndpoint("https").Url);
});

builder.Build().Run();