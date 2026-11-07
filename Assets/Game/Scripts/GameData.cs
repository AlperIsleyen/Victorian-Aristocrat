using System;
using System.Collections.Generic;
using UnityEngine;

namespace VictorianAristocrat
{
    public enum GoodKind { Raw, Food, Manufactured, Arms }
    public enum CrisisKind { None, War, Famine }
    public enum RouteKind { Road, Rail, Sea }

    [Serializable] public class GoodDef
    {
        public string id;
        public string displayName;
        public GoodKind kind;
        public int basePrice;
        public int weight = 1;
        public bool extractable;
    }

    [Serializable] public class CountryDef
    {
        public string name;
        public Color primary;
        public Color secondary;
    }

    [Serializable] public class SettlementDef
    {
        public int id;
        public int region;
        public int country;
        public string displayName;
        public bool city;
        public string specialty;
        public CrisisKind initialCrisis;
        public int crisisSeverity;
        public bool station;
        public bool port;
        public bool island;
        public Vector2 position;
    }

    [Serializable] public class RouteDef
    {
        public int from;
        public int to;
        public RouteKind kind;
        public int days;
        public int fare;
        public int capacity;
    }

    [Serializable] public class StockEntry { public string good; public int amount; }
    [Serializable] public class SettlementState
    {
        public int id;
        public List<StockEntry> stock = new List<StockEntry>();
        public int relationship;
        public CrisisKind crisis;
        public int severity;
        public int warProgress;
    }
    [Serializable] public class LicenseState { public int settlement; public string good; public int level = 1; public int pending; }
    [Serializable] public class GameState
    {
        public int version = 2;
        public int day = 1;
        public int settlement = 0;
        public bool hasWorldPosition;
        public Vector3 worldPosition;
        public int money = 450;
        public int wagons = 1;
        public int attachedWagons = 1;
        public List<StockEntry> cargo = new List<StockEntry>();
        public List<SettlementState> settlements = new List<SettlementState>();
        public List<LicenseState> licenses = new List<LicenseState>();
    }
}
