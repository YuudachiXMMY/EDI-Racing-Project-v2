using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

[TestFixture]
public class EventManagerTests
{
    private GameObject managerObj;
    private EventManager eventManager;
    private EventSchedule schedule;
    private List<GameObject> carObjects;

    [SetUp]
    public void SetUp()
    {
        managerObj = new GameObject("EventManager");
        eventManager = managerObj.AddComponent<EventManager>();

        schedule = ScriptableObject.CreateInstance<EventSchedule>();
        schedule.Events = new EventRule[]
        {
            new EventRule
            {
                DisplayName = "Test Event",
                AttributeName = "colorIndex",
                Operator = ComparisonOperator.Equals,
                CompareValue = "3",
                SpeedDelta = -10f,
                Duration = 8f,
                Weather = WeatherType.None,
                TriggerKey = Key.Digit1,
                AllowRepeat = false
            },
            new EventRule
            {
                DisplayName = "Repeatable Event",
                AttributeName = "",
                Operator = ComparisonOperator.All,
                CompareValue = "",
                SpeedDelta = -5f,
                Duration = 5f,
                Weather = WeatherType.Snow,
                TriggerKey = Key.Digit2,
                AllowRepeat = true
            }
        };
        eventManager.Schedule = schedule;

        carObjects = new List<GameObject>();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in carObjects)
            UnityEngine.Object.DestroyImmediate(obj);
        UnityEngine.Object.DestroyImmediate(managerObj);
        UnityEngine.Object.DestroyImmediate(schedule);
    }

    private CarIdentity CreateCar(string name, AttributeEntry[] attrs)
    {
        var obj = new GameObject(name);
        carObjects.Add(obj);
        var identity = obj.AddComponent<CarIdentity>();
        identity.Initialize(new CarData(name, attrs));
        return identity;
    }

    [Test]
    public void RegisterCar_IncreasesCount()
    {
        var car = CreateCar("A", Array.Empty<AttributeEntry>());

        eventManager.RegisterCar(car);

        Assert.AreEqual(1, eventManager.RegisteredCarCount);
    }

    [Test]
    public void RegisterCars_RegistersAllWithIdentity()
    {
        var cars = new List<GameObject>();
        for (int i = 0; i < 3; i++)
        {
            var obj = new GameObject($"Car{i}");
            carObjects.Add(obj);
            var identity = obj.AddComponent<CarIdentity>();
            identity.Initialize(new CarData($"Team{i}", Array.Empty<AttributeEntry>()));
            cars.Add(obj);
        }

        eventManager.RegisterCars(cars);

        Assert.AreEqual(3, eventManager.RegisteredCarCount);
    }

    [Test]
    public void Activate_SetsIsActive()
    {
        Assert.IsFalse(eventManager.IsActive);

        eventManager.Activate();

        Assert.IsTrue(eventManager.IsActive);
    }

    [Test]
    public void Deactivate_ClearsIsActive()
    {
        eventManager.Activate();
        eventManager.Deactivate();

        Assert.IsFalse(eventManager.IsActive);
    }

    [Test]
    public void TriggerEvent_ValidIndex_SetsHasBeenTriggered()
    {
        var car = CreateCar("A", Array.Empty<AttributeEntry>());
        eventManager.RegisterCar(car);

        eventManager.TriggerEvent(0);

        Assert.IsTrue(schedule.Events[0].HasBeenTriggered);
    }

    [Test]
    public void TriggerEvent_InvalidIndex_Negative_NoError()
    {
        Assert.DoesNotThrow(() => eventManager.TriggerEvent(-1));
    }

    [Test]
    public void TriggerEvent_InvalidIndex_OutOfRange_NoError()
    {
        Assert.DoesNotThrow(() => eventManager.TriggerEvent(999));
    }

    [Test]
    public void TriggerEvent_NoRepeat_SecondTriggerIgnored()
    {
        eventManager.RegisterCar(CreateCar("A", Array.Empty<AttributeEntry>()));

        int eventCount = 0;
        eventManager.OnEventTriggered += (rule, count) => eventCount++;

        eventManager.TriggerEvent(0); // AllowRepeat = false
        eventManager.TriggerEvent(0); // should be skipped

        Assert.AreEqual(1, eventCount);
    }

    [Test]
    public void TriggerEvent_AllowRepeat_TriggersAgain()
    {
        eventManager.RegisterCar(CreateCar("A", Array.Empty<AttributeEntry>()));

        int eventCount = 0;
        eventManager.OnEventTriggered += (rule, count) => eventCount++;

        eventManager.TriggerEvent(1); // AllowRepeat = true
        eventManager.TriggerEvent(1);

        Assert.AreEqual(2, eventCount);
    }

    [Test]
    public void TriggerEvent_OnEventTriggered_FiresWithCorrectRule()
    {
        eventManager.RegisterCar(CreateCar("A", Array.Empty<AttributeEntry>()));

        EventRule firedRule = default;
        eventManager.OnEventTriggered += (rule, count) => firedRule = rule;

        eventManager.TriggerEvent(0);

        Assert.AreEqual("Test Event", firedRule.DisplayName);
    }

    [Test]
    public void TriggerEventByName_FindsByName_CaseInsensitive()
    {
        eventManager.RegisterCar(CreateCar("A", Array.Empty<AttributeEntry>()));

        bool fired = false;
        eventManager.OnEventTriggered += (rule, count) => fired = true;

        eventManager.TriggerEventByName("test event");

        Assert.IsTrue(fired);
    }

    [Test]
    public void TriggerEventByName_UnknownName_NoError()
    {
        Assert.DoesNotThrow(() => eventManager.TriggerEventByName("Nonexistent"));
    }

    [Test]
    public void ClearRegisteredCars_ResetsState()
    {
        eventManager.RegisterCar(CreateCar("A", Array.Empty<AttributeEntry>()));
        eventManager.Activate();

        eventManager.ClearRegisteredCars();

        Assert.AreEqual(0, eventManager.RegisteredCarCount);
        Assert.IsFalse(eventManager.IsActive);
    }
}
