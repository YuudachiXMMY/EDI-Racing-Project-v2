using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class RuleEngineTests
{
    private GameObject testObj;
    private CarIdentity testCar;

    [SetUp]
    public void SetUp()
    {
        testObj = new GameObject("TestCar");
        testCar = testObj.AddComponent<CarIdentity>();
        testCar.Initialize(new CarData("TestTeam", new AttributeEntry[]
        {
            new AttributeEntry { Key = "colorIndex", Value = "2" },
            new AttributeEntry { Key = "functions", Value = "password/glasses/facerecog" },
            new AttributeEntry { Key = "language", Value = "French" },
            new AttributeEntry { Key = "score", Value = "7" },
            new AttributeEntry { Key = "shortName", Value = "Hi" }
        }));
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(testObj);
    }

    [Test]
    public void IsAffected_AllOperator_AlwaysReturnsTrue()
    {
        var rule = new EventRule { Operator = ComparisonOperator.All };
        Assert.IsTrue(RuleEngine.IsAffected(rule, testCar));
    }

    [Test]
    public void IsAffected_Equals_MatchesExact()
    {
        var rule = new EventRule
        {
            AttributeName = "colorIndex",
            Operator = ComparisonOperator.Equals,
            CompareValue = "2"
        };
        Assert.IsTrue(RuleEngine.IsAffected(rule, testCar));
    }

    [Test]
    public void IsAffected_Equals_CaseInsensitive()
    {
        var rule = new EventRule
        {
            AttributeName = "language",
            Operator = ComparisonOperator.Equals,
            CompareValue = "french"
        };
        Assert.IsTrue(RuleEngine.IsAffected(rule, testCar));
    }

    [Test]
    public void IsAffected_NotEquals_ReturnsTrueWhenDifferent()
    {
        var rule = new EventRule
        {
            AttributeName = "language",
            Operator = ComparisonOperator.NotEquals,
            CompareValue = "English"
        };
        Assert.IsTrue(RuleEngine.IsAffected(rule, testCar));
    }

    [Test]
    public void IsAffected_Contains_MatchesSlashSeparatedList()
    {
        var rule = new EventRule
        {
            AttributeName = "functions",
            Operator = ComparisonOperator.Contains,
            CompareValue = "glasses"
        };
        Assert.IsTrue(RuleEngine.IsAffected(rule, testCar));
    }

    [Test]
    public void IsAffected_Contains_MatchesSubstring()
    {
        var rule = new EventRule
        {
            AttributeName = "language",
            Operator = ComparisonOperator.Contains,
            CompareValue = "rench"
        };
        Assert.IsTrue(RuleEngine.IsAffected(rule, testCar));
    }

    [Test]
    public void IsAffected_NotContains_ReturnsTrueWhenAbsent()
    {
        var rule = new EventRule
        {
            AttributeName = "functions",
            Operator = ComparisonOperator.NotContains,
            CompareValue = "fingerprint"
        };
        Assert.IsTrue(RuleEngine.IsAffected(rule, testCar));
    }

    [Test]
    public void IsAffected_GreaterThan_NumericComparison()
    {
        var rule = new EventRule
        {
            AttributeName = "score",
            Operator = ComparisonOperator.GreaterThan,
            CompareValue = "5"
        };
        Assert.IsTrue(RuleEngine.IsAffected(rule, testCar));
    }

    [Test]
    public void IsAffected_LessThan_NumericComparison()
    {
        var rule = new EventRule
        {
            AttributeName = "score",
            Operator = ComparisonOperator.LessThan,
            CompareValue = "10"
        };
        Assert.IsTrue(RuleEngine.IsAffected(rule, testCar));
    }

    [Test]
    public void IsAffected_LengthGreaterThan_ChecksStringLength()
    {
        var rule = new EventRule
        {
            AttributeName = "language",
            Operator = ComparisonOperator.LengthGreaterThan,
            CompareValue = "3"
        };
        Assert.IsTrue(RuleEngine.IsAffected(rule, testCar)); // "French" len=6 > 3
    }

    [Test]
    public void IsAffected_LengthLessThan_ChecksStringLength()
    {
        var rule = new EventRule
        {
            AttributeName = "shortName",
            Operator = ComparisonOperator.LengthLessThan,
            CompareValue = "5"
        };
        Assert.IsTrue(RuleEngine.IsAffected(rule, testCar)); // "Hi" len=2 < 5
    }

    [Test]
    public void IsAffected_MissingAttribute_UsesEmptyString()
    {
        var rule = new EventRule
        {
            AttributeName = "nonexistent",
            Operator = ComparisonOperator.Equals,
            CompareValue = ""
        };
        Assert.IsTrue(RuleEngine.IsAffected(rule, testCar));
    }

    [Test]
    public void IsAffected_TeamNameAttribute_ResolvesCorrectly()
    {
        var rule = new EventRule
        {
            AttributeName = "teamName",
            Operator = ComparisonOperator.Equals,
            CompareValue = "TestTeam"
        };
        Assert.IsTrue(RuleEngine.IsAffected(rule, testCar));
    }

    [Test]
    public void IsAffected_NonNumericForGreaterThan_ReturnsFalse()
    {
        var rule = new EventRule
        {
            AttributeName = "language",
            Operator = ComparisonOperator.GreaterThan,
            CompareValue = "5"
        };
        // "French" can't be parsed as number, CompareNumeric returns 0
        Assert.IsFalse(RuleEngine.IsAffected(rule, testCar));
    }

    // --- Compound condition tests ---

    [Test]
    public void IsAffected_AndConditions_AllMatch_ReturnsTrue()
    {
        var rule = new EventRule
        {
            Logic = LogicOperator.And,
            Conditions = new[]
            {
                new RuleCondition { AttributeName = "colorIndex", Operator = ComparisonOperator.Equals, CompareValue = "2" },
                new RuleCondition { AttributeName = "language", Operator = ComparisonOperator.Equals, CompareValue = "French" }
            }
        };
        Assert.IsTrue(RuleEngine.IsAffected(rule, testCar));
    }

    [Test]
    public void IsAffected_AndConditions_OneFails_ReturnsFalse()
    {
        var rule = new EventRule
        {
            Logic = LogicOperator.And,
            Conditions = new[]
            {
                new RuleCondition { AttributeName = "colorIndex", Operator = ComparisonOperator.Equals, CompareValue = "2" },
                new RuleCondition { AttributeName = "language", Operator = ComparisonOperator.Equals, CompareValue = "English" }
            }
        };
        Assert.IsFalse(RuleEngine.IsAffected(rule, testCar));
    }

    [Test]
    public void IsAffected_OrConditions_OneMatches_ReturnsTrue()
    {
        var rule = new EventRule
        {
            Logic = LogicOperator.Or,
            Conditions = new[]
            {
                new RuleCondition { AttributeName = "colorIndex", Operator = ComparisonOperator.Equals, CompareValue = "5" },
                new RuleCondition { AttributeName = "language", Operator = ComparisonOperator.Equals, CompareValue = "French" }
            }
        };
        Assert.IsTrue(RuleEngine.IsAffected(rule, testCar));
    }

    [Test]
    public void IsAffected_OrConditions_NoneMatch_ReturnsFalse()
    {
        var rule = new EventRule
        {
            Logic = LogicOperator.Or,
            Conditions = new[]
            {
                new RuleCondition { AttributeName = "colorIndex", Operator = ComparisonOperator.Equals, CompareValue = "5" },
                new RuleCondition { AttributeName = "language", Operator = ComparisonOperator.Equals, CompareValue = "English" }
            }
        };
        Assert.IsFalse(RuleEngine.IsAffected(rule, testCar));
    }

    [Test]
    public void IsAffected_EmptyConditions_FallsBackToLegacy()
    {
        var rule = new EventRule
        {
            Conditions = new RuleCondition[0],
            AttributeName = "colorIndex",
            Operator = ComparisonOperator.Equals,
            CompareValue = "2"
        };
        Assert.IsTrue(RuleEngine.IsAffected(rule, testCar));
    }

    [Test]
    public void IsAffected_NullConditions_FallsBackToLegacy()
    {
        var rule = new EventRule
        {
            Conditions = null,
            AttributeName = "score",
            Operator = ComparisonOperator.GreaterThan,
            CompareValue = "5"
        };
        Assert.IsTrue(RuleEngine.IsAffected(rule, testCar));
    }

    [Test]
    public void IsAffected_SingleConditionInArray_WorksLikeSimple()
    {
        var rule = new EventRule
        {
            Logic = LogicOperator.And,
            Conditions = new[]
            {
                new RuleCondition { AttributeName = "colorIndex", Operator = ComparisonOperator.Equals, CompareValue = "2" }
            }
        };
        Assert.IsTrue(RuleEngine.IsAffected(rule, testCar));
    }

    [Test]
    public void IsAffected_AllOperator_IgnoresConditions()
    {
        var rule = new EventRule
        {
            Operator = ComparisonOperator.All,
            Logic = LogicOperator.And,
            Conditions = new[]
            {
                new RuleCondition { AttributeName = "colorIndex", Operator = ComparisonOperator.Equals, CompareValue = "999" }
            }
        };
        Assert.IsTrue(RuleEngine.IsAffected(rule, testCar));
    }

    [Test]
    public void IsAffected_AndConditions_MixedOperators_Works()
    {
        var rule = new EventRule
        {
            Logic = LogicOperator.And,
            Conditions = new[]
            {
                new RuleCondition { AttributeName = "colorIndex", Operator = ComparisonOperator.Equals, CompareValue = "2" },
                new RuleCondition { AttributeName = "score", Operator = ComparisonOperator.GreaterThan, CompareValue = "5" },
                new RuleCondition { AttributeName = "functions", Operator = ComparisonOperator.Contains, CompareValue = "glasses" }
            }
        };
        Assert.IsTrue(RuleEngine.IsAffected(rule, testCar));
    }
}
