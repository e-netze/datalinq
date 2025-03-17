using Microsoft.CodeAnalysis;

namespace E.DataLinq.LanguageEngine.Razor.Exceptions;

public class RazorEngineCompilationException : RazorEngineException
{
    public RazorEngineCompilationException(
            List<Diagnostic> errors,
            string generatedCode
        ) : base("")
        => (Errors, GeneratedCode) = (errors, generatedCode);


    public List<Diagnostic> Errors { get; set; }

    public string GeneratedCode { get; set; } = "";

    public override string Message
    {
        get
        {
            string errors = string.Join("\n", Errors.Where(w => w.IsWarningAsError || w.Severity == DiagnosticSeverity.Error));
            return "Unable to compile template: " + errors;
        }
    }
}
