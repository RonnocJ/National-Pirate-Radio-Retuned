using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStats : Singleton<PlayerStats>, ISaveData
{
    [Header("Player Progress")]
    public bool NewGame = true;
    public int Runs = 0;
    public int EnemyKills = 0;
    [Header("Upgradable Stats")]
    public float VehicleSpeed = 1;
    public float WeaponDamage = 1;
    public float AbilityStrength = 1;
    public float RegenAmount = 10;
    public float RegenDelay = 2.5f;
    public float MaxHype = 100f;
    [Header("Money Stats")]
    public float CurrentMoney = 0f;
    public float LastRunMoney = 0f;
    public float FCCFine = 50f;
    public float VanUpkeep = 25f;
    public Dictionary<string, object> AddSaveData()
    {
        return new Dictionary<string, object>()
        {
            { "newGame", NewGame },
            { "runs", Runs },
            {"enemyKills", EnemyKills },

            { "vehicleSpeed", VehicleSpeed },
            { "weaponDamage", WeaponDamage },
            { "abilityStrength", AbilityStrength },
            { "regenAmount", RegenAmount },
            { "regenDelay", RegenDelay },
            { "maxHype", MaxHype },

            { "currentMoney", CurrentMoney },
            { "lastRunMoney", LastRunMoney },
            {"fccFine", FCCFine },
            {"vanUpkeep", VanUpkeep },
        };
    }
    public void ReadSaveData(Dictionary<string, object> dataDict)
    {
        if (dataDict.TryGetValue("newGame", out object newGame)) NewGame = Convert.ToBoolean(newGame);

        if (dataDict.TryGetValue("runs", out object runs)) Runs = Convert.ToInt32(runs);

        if (dataDict.TryGetValue("enemyKills", out object enemyKills)) EnemyKills = Convert.ToInt32(enemyKills);

        if (dataDict.TryGetValue("vehicleSpeed", out object vehicleSpeed)) VehicleSpeed = Convert.ToSingle(vehicleSpeed);

        if (dataDict.TryGetValue("weaponDamage", out object weaponDamage)) WeaponDamage = Convert.ToSingle(weaponDamage);

        if (dataDict.TryGetValue("abilityStrength", out object abilityStrength)) AbilityStrength = Convert.ToSingle(abilityStrength);

        if (dataDict.TryGetValue("regenAmount", out object regenAmount)) RegenAmount = Convert.ToSingle(regenAmount);

        if (dataDict.TryGetValue("regenDelay", out object regenDelay)) RegenDelay = Convert.ToSingle(regenDelay);

        if (dataDict.TryGetValue("maxHype", out object maxHype)) MaxHype = Convert.ToSingle(maxHype);

        if (dataDict.TryGetValue("currentMoney", out object currentMoney)) CurrentMoney = Convert.ToSingle(currentMoney);

        if (dataDict.TryGetValue("lastRunMoney", out object lastRunMoney)) LastRunMoney = Convert.ToSingle(lastRunMoney);

        if (dataDict.TryGetValue("fccFine", out object fccFine)) FCCFine = Convert.ToSingle(fccFine);

        if (dataDict.TryGetValue("vanUpkeep", out object vanUpkeep)) VanUpkeep = Convert.ToSingle(vanUpkeep);
    }
}