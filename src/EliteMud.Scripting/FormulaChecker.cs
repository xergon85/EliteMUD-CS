using System.Reflection;

namespace EliteMud.Scripting;

/// <summary>
/// Checks every content formula when content loads instead of in the middle of a fight:
/// - statically, every name it reads must be one of its slot's variables or a built-in (all branches)
/// - one sample run catches syntax errors and the result type of the branch that runs
/// The result type of branches the sample doesn't take can't be guaranteed; the content editor's live
/// preview covers that.
/// </summary>
public static class FormulaChecker
{
    /// <summary>Returns one message per broken formula; empty when all are fine.</summary>
    public static IReadOnlyList<string> Check(FormulaEvaluator evaluator, IEnumerable<FormulaSlot> slots)
    {
        var errors = new List<string>();
        foreach (var slot in slots.Where(slot => !string.IsNullOrWhiteSpace(slot.Formula)))
        {
            // Static check first: catches unknown names in every branch, not just the one the sample run takes
            var allowed = AllowedNames(slot.InputsType);
            var unknown = FormulaNames.FindUnknown(slot.Formula!, allowed);
            if (unknown.Count > 0)
            {
                errors.Add($"formula {slot.Name} (\"{slot.Formula}\") reads unknown variable(s) "
                           + $"{string.Join(", ", unknown.Select(name => $"'{name}'"))}; "
                           + $"available: {string.Join(", ", allowed.Order(StringComparer.Ordinal))}");
                continue;
            }

            try
            {
                var inputs = CreateSample(slot.InputsType);
                if (slot.Result == FormulaResult.Number)
                {
                    evaluator.EvaluateInt(slot.Formula!, inputs, slot.Name);
                }
                else
                {
                    evaluator.EvaluateBool(slot.Formula!, inputs, slot.Name);
                }
            }
            catch (FormulaEvaluationException ex)
            {
                errors.Add(ex.Message);
            }
        }

        return errors;
    }

    /// <summary>The slot's variables (camelCase input properties) plus the built-ins.</summary>
    public static IReadOnlySet<string> AllowedNames(Type inputsType) =>
        inputsType.GetProperties()
            .Select(property => FormulaEvaluator.ToVariableName(property.Name))
            .Concat(FormulaEvaluator.BuiltInNames)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Build an input record with sample values: numbers are 1 (so div can't divide by zero), booleans true,
    /// strings "x", enums their first value.
    /// </summary>
    public static object CreateSample(Type inputsType)
    {
        var constructor = inputsType
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(ctor => ctor.GetParameters().Length)
            .First();

        var arguments = constructor.GetParameters().Select(parameter => SampleValue(parameter.ParameterType)).ToArray();
        return constructor.Invoke(arguments);
    }

    private static object? SampleValue(Type type) => type switch
    {
        _ when type == typeof(int) => 1,
        _ when type == typeof(long) => 1L,
        _ when type == typeof(double) => 1.0,
        _ when type == typeof(bool) => true,
        _ when type == typeof(string) => "x",
        { IsEnum: true } => Enum.GetValues(type).GetValue(0),
        _ => throw new NotSupportedException($"Formula inputs can't use {type.Name}; use numbers, booleans, strings or enums.")
    };
}
