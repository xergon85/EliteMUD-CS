using EliteMud.Scripting;

namespace EliteMud.Tests.Scripting;

public class FormulaEvaluatorTests
{
    private readonly FormulaEvaluator _evaluator;

    public FormulaEvaluatorTests()
    {
        _evaluator = new FormulaEvaluator();
    }

    [Fact]
    public void EvaluateInt_SimpleFormula_ReturnsCorrectValue()
    {
        // Arrange
        var formula = "return 5 + 3";

        // Act
        var result = _evaluator.EvaluateInt(formula, new { });

        // Assert
        Assert.Equal(8, result);
    }

    [Fact]
    public void EvaluateInt_WithContextVariable_ReturnsCorrectValue()
    {
        // Arrange
        var formula = "return level * 2";

        // Act
        var result = _evaluator.EvaluateInt(formula, new { level = 10 });

        // Assert
        Assert.Equal(20, result);
    }

    [Fact]
    public void EvaluateInt_KickDamageFormula_ReturnsCorrectValue()
    {
        // Arrange - Legacy kick damage: level / 2
        var formula = "return math.max(1, level / 2)";

        // Act
        var result = _evaluator.EvaluateInt(formula, new { level = 20 });

        // Assert
        Assert.Equal(10, result);
    }

    [Fact]
    public void EvaluateInt_BackstabMultiplierFormula_ReturnsCorrectValue()
    {
        // Arrange - Legacy backstab: MIN(level/10 + 1, 5)
        var formula = "return math.min(level / 10 + 1, 5)";

        // Act - Level 50 should give multiplier of 5
        var result = _evaluator.EvaluateInt(formula, new { level = 50 });

        // Assert
        Assert.Equal(5, result);
    }

    [Fact]
    public void EvaluateInt_BackstabMultiplierFormula_ClampsAtFive()
    {
        // Arrange
        var formula = "return math.min(level / 10 + 1, 5)";

        // Act - Level 10 should give multiplier of 2
        var result = _evaluator.EvaluateInt(formula, new { level = 10 });

        // Assert
        Assert.Equal(2, result);
    }

    [Fact]
    public void EvaluateInt_WithRandomFunction_ReturnsValueInRange()
    {
        // Arrange
        var formula = "return random(1, 10)";

        // Act
        var result = _evaluator.EvaluateInt(formula, new { });

        // Assert
        Assert.InRange(result, 1, 10);
    }

    [Fact]
    public void EvaluateInt_MultipleVariables_ReturnsCorrectValue()
    {
        // Arrange
        var formula = "return (victimAC + level) / 2";

        // Act
        var result = _evaluator.EvaluateInt(formula, new { victimAC = 50, level = 10 });

        // Assert
        Assert.Equal(30, result);
    }

    [Fact]
    public void EvaluateBool_SimpleComparison_ReturnsTrue()
    {
        // Arrange
        var formula = "return level > 5";

        // Act
        var result = _evaluator.EvaluateBool(formula, new { level = 10 });

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void EvaluateBool_SimpleComparison_ReturnsFalse()
    {
        // Arrange
        var formula = "return level > 5";

        // Act
        var result = _evaluator.EvaluateBool(formula, new { level = 3 });

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void EvaluateBool_KickHitFormula_ReturnsBoolean()
    {
        // Arrange - Legacy kick: ((10 - AC / 10) << 1) + number(1, 101) <= skill (C integer division)
        var formula = "return ((10 - div(victimAC, 10)) * 2) + random(1, 101) <= skillPercent";

        // Act - High skill should often hit
        var result = _evaluator.EvaluateBool(formula, new { victimAC = 100, skillPercent = 95 });

        // Assert
        Assert.IsType<bool>(result);
    }

    [Fact]
    public void EvaluateBool_DodgeActivationFormula_ReturnsBoolean()
    {
        // Arrange - Legacy dodge: (random(1, 250) + damage) < skillPercent
        var formula = "return (random(1, 250) + damage) < skillPercent";

        // Act
        var result = _evaluator.EvaluateBool(formula, new { damage = 10, skillPercent = 95 });

        // Assert
        Assert.IsType<bool>(result);
    }

    [Fact]
    public void EvaluateInt_InvalidFormula_ThrowsException()
    {
        // Arrange - formula missing 'return' is treated as syntax error by MoonSharp
        var formula = "level / 2";

        // Act & Assert
        var exception = Assert.Throws<FormulaEvaluationException>(() =>
            _evaluator.EvaluateInt(formula, new { level = 10 }));

        Assert.Contains("syntax error", exception.Message);
    }

    [Fact]
    public void EvaluateInt_UndefinedVariable_ThrowsException()
    {
        // Arrange - references undefined variable
        var formula = "return undefinedVar * 2";

        // Act & Assert
        var exception = Assert.Throws<FormulaEvaluationException>(() =>
            _evaluator.EvaluateInt(formula, new { level = 10 }));

        Assert.Contains("runtime error", exception.Message);
    }

    [Fact]
    public void EvaluateInt_ReturnsWrongType_ThrowsException()
    {
        // Arrange - returns boolean instead of number
        var formula = "return true";

        // Act & Assert
        var exception = Assert.Throws<FormulaEvaluationException>(() =>
            _evaluator.EvaluateInt(formula, new { }));

        Assert.Contains("expected a number", exception.Message);
    }

    [Fact]
    public void EvaluateBool_ReturnsWrongType_ThrowsException()
    {
        // Arrange - returns number instead of boolean
        var formula = "return 42";

        // Act & Assert
        var exception = Assert.Throws<FormulaEvaluationException>(() =>
            _evaluator.EvaluateBool(formula, new { }));

        Assert.Contains("expected true or false", exception.Message);
    }

    [Fact]
    public void Evaluate_UnknownVariable_ThrowsWithItsName()
    {
        // A typo must fail instead of reading nil or a leftover value
        var exception = Assert.Throws<FormulaEvaluationException>(() =>
            _evaluator.EvaluateInt("return victimAc * 2", new { victimAC = 10 }, "kick.hitFormula"));

        Assert.Contains("unknown variable 'victimAc'", exception.Message);
        Assert.Contains("kick.hitFormula", exception.Message);
    }

    [Fact]
    public void Evaluate_VariablesDoNotLeakBetweenFormulas()
    {
        // First formula provides 'level'; the second doesn't, so it must not see the old value
        _evaluator.EvaluateInt("return level", new { level = 50 });

        Assert.Throws<FormulaEvaluationException>(() => _evaluator.EvaluateInt("return level", new { }));
    }

    [Fact]
    public void Evaluate_BuiltInsStayAvailable()
    {
        Assert.Equal(7, _evaluator.EvaluateInt("return math.max(1, math.floor(15 / 2))", new { }));
        Assert.InRange(_evaluator.EvaluateInt("return random(1, 6)", new { }), 1, 6);
    }

    [Theory]
    [InlineData(55, 10, 5)]
    [InlineData(-55, 10, -5)]  // C truncates toward zero; math.floor would give -6
    [InlineData(7, -2, -3)]
    [InlineData(18, 4, 4)]
    public void Div_MatchesCIntegerDivision(int a, int b, int expected)
    {
        Assert.Equal(expected, _evaluator.EvaluateInt("return div(a, b)", new { a, b }));
    }

    [Fact]
    public void Div_ByZero_Throws()
    {
        Assert.Throws<FormulaEvaluationException>(() => _evaluator.EvaluateInt("return div(1, 0)", new { }));
    }

    [Theory]
    [InlineData("return io.open('x')")]
    [InlineData("return os.execute('ls')")]
    [InlineData("return load('return 1')()")]
    [InlineData("return require('io')")]
    [InlineData("return string.len('x')")]
    public void Evaluate_OnlyBuiltInsAreReachable(string formula)
    {
        // Formulas get math, random and div, nothing else: no files, OS commands or code loading
        var exception = Assert.Throws<FormulaEvaluationException>(() => _evaluator.EvaluateInt(formula, new { }));

        Assert.Contains("unknown variable", exception.Message);
    }

    [Fact]
    public void Evaluate_MathIsReadOnly()
    {
        var exception = Assert.Throws<FormulaEvaluationException>(() =>
            _evaluator.EvaluateBool("math.floor = function() return 999 end return true", new { }));

        Assert.Contains("math is read-only", exception.Message);
        Assert.Equal(2, _evaluator.EvaluateInt("return math.floor(2.5)", new { })); // other formulas unaffected
    }

    [Fact]
    public void ValidateFormula_ValidFormula_DoesNotThrow()
    {
        // Arrange
        var formula = "return level / 2";

        // Act & Assert - should not throw
        _evaluator.ValidateFormula(formula);
    }

    [Fact]
    public void ValidateFormula_InvalidSyntax_ThrowsException()
    {
        // Arrange - syntax error: unclosed parenthesis
        var formula = "return (level / 2";

        // Act & Assert
        var exception = Assert.Throws<FormulaEvaluationException>(() =>
            _evaluator.ValidateFormula(formula));

        Assert.Contains("Invalid formula syntax", exception.Message);
    }

    [Fact]
    public void ValidateFormula_EmptyFormula_ThrowsException()
    {
        // Arrange
        var formula = "";

        // Act & Assert
        var exception = Assert.Throws<FormulaEvaluationException>(() =>
            _evaluator.ValidateFormula(formula));

        Assert.Contains("cannot be null or empty", exception.Message);
    }

    [Fact]
    public void EvaluateInt_MultipleSequentialCalls_DoesNotLeakContext()
    {
        // Arrange
        var formula1 = "return level";
        var formula2 = "return level";

        // Act - Call with different contexts
        var result1 = _evaluator.EvaluateInt(formula1, new { level = 10 });
        var result2 = _evaluator.EvaluateInt(formula2, new { level = 50 });

        // Assert - Each call should use its own context
        Assert.Equal(10, result1);
        Assert.Equal(50, result2);
    }

    [Fact]
    public void EvaluateInt_RandomFunction_GeneratesDifferentValues()
    {
        // Arrange
        var formula = "return random(1, 100)";
        var results = new HashSet<int>();

        // Act - Call multiple times
        for (int i = 0; i < 20; i++)
        {
            results.Add(_evaluator.EvaluateInt(formula, new { }));
        }

        // Assert - Should generate at least some different values (probabilistic test)
        Assert.True(results.Count > 1, "Random should generate different values");
    }
}
