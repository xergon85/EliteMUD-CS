using System.Reflection;

namespace EliteMud.Scripting;

/// <summary>
/// Runs every content formula once with sample inputs, so syntax errors, unknown variables and wrong result
/// types are found when content loads instead of in the middle of a fight.
/// </summary>
public static class FormulaChecker
{
    /// <summary>Returns one message per broken formula; empty when all are fine.</summary>
    public static IReadOnlyList<string> Check(FormulaEvaluator evaluator, IEnumerable<FormulaSlot> slots)
    {
        var errors = new List<string>();
        foreach (var slot in slots.Where(slot => !string.IsNullOrWhiteSpace(slot.Formula)))
        {
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
