using NUnit.Framework;

[TestFixture]
public class EventActionBuilderTests
{
    [Test]
    public void test_constants_match_approved_values()
    {
        Assert.AreEqual(20f, EventActionBuilder.BoostDelta);
        Assert.AreEqual(-15f, EventActionBuilder.PenaltyDelta);
        Assert.AreEqual(10f, EventActionBuilder.EffectDuration);
    }

    [Test]
    public void test_name_length_penalty_builds_length_rule()
    {
        // Act
        var rule = EventActionBuilder.NameLengthPenalty(7);

        // Assert
        Assert.AreEqual("teamName", rule.AttributeName);
        Assert.AreEqual(ComparisonOperator.LengthGreaterThan, rule.Operator);
        Assert.AreEqual("7", rule.CompareValue);
        Assert.AreEqual(-15f, rule.SpeedDelta);
        Assert.AreEqual(10f, rule.Duration);
        Assert.AreEqual(WeatherType.None, rule.Weather);
    }

    [Test]
    public void test_color_boost_builds_equals_colorindex_rule()
    {
        var rule = EventActionBuilder.Color(3, boost: true);

        Assert.AreEqual("colorIndex", rule.AttributeName);
        Assert.AreEqual(ComparisonOperator.Equals, rule.Operator);
        Assert.AreEqual("3", rule.CompareValue);
        Assert.AreEqual(20f, rule.SpeedDelta);
        Assert.AreEqual(10f, rule.Duration);
    }

    [Test]
    public void test_color_penalty_uses_penalty_delta()
    {
        var rule = EventActionBuilder.Color(2, boost: false);

        Assert.AreEqual("2", rule.CompareValue);
        Assert.AreEqual(-15f, rule.SpeedDelta);
    }

    [Test]
    public void test_function_penalty_builds_contains_functions_rule()
    {
        var rule = EventActionBuilder.Function("facerecog", boost: false);

        Assert.AreEqual("functions", rule.AttributeName);
        Assert.AreEqual(ComparisonOperator.Contains, rule.Operator);
        Assert.AreEqual("facerecog", rule.CompareValue);
        Assert.AreEqual(-15f, rule.SpeedDelta);
    }

    [Test]
    public void test_function_boost_uses_boost_delta()
    {
        var rule = EventActionBuilder.Function("password", boost: true);

        Assert.AreEqual("password", rule.CompareValue);
        Assert.AreEqual(20f, rule.SpeedDelta);
    }

    [Test]
    public void test_male_boost_matches_male_tag_with_boost_delta()
    {
        var rule = EventActionBuilder.Male(accelerate: true);

        Assert.AreEqual("functions", rule.AttributeName);
        Assert.AreEqual(ComparisonOperator.Contains, rule.Operator);
        Assert.AreEqual("male", rule.CompareValue);
        Assert.AreEqual(20f, rule.SpeedDelta);
    }

    [Test]
    public void test_male_penalty_uses_penalty_delta()
    {
        var rule = EventActionBuilder.Male(accelerate: false);

        Assert.AreEqual("male", rule.CompareValue);
        Assert.AreEqual(-15f, rule.SpeedDelta);
    }

    [Test]
    public void test_snow_builds_all_operator_snow_weather()
    {
        var rule = EventActionBuilder.Snow();

        Assert.AreEqual(ComparisonOperator.All, rule.Operator);
        Assert.AreEqual(WeatherType.Snow, rule.Weather);
        Assert.AreEqual(-8f, rule.SpeedDelta);
        Assert.AreEqual(12f, rule.Duration);
    }

    [Test]
    public void test_night_builds_all_operator_night_weather()
    {
        var rule = EventActionBuilder.Night();

        Assert.AreEqual(ComparisonOperator.All, rule.Operator);
        Assert.AreEqual(WeatherType.Night, rule.Weather);
        Assert.AreEqual(-5f, rule.SpeedDelta);
        Assert.AreEqual(15f, rule.Duration);
    }

    [Test]
    public void test_pick_lists_have_five_entries_each()
    {
        Assert.AreEqual(5, EventActionBuilder.Functions.Length);
        Assert.AreEqual(5, EventActionBuilder.Colors.Length);
    }
}
