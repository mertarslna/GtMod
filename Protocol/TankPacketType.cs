namespace GtProxyMod.Protocol
{
    public enum TankPacketType : byte
    {
        State = 0,
        CallFunction = 1,
        UpdateStatus = 2,
        TileChangeReq = 3,
        SendMapData = 4,
        SendTileUpdateData = 5,
        SendTileUpdateDataMultiple = 6,
        TileActivateReq = 7,
        TileApplyDamage = 8,
        SendInventoryState = 9,
        ItemActivateReq = 10,
        ItemActivateObjectReq = 11,
        SendTileTreeState = 12,
        ModifyItemInventory = 13,
        ItemChangeObject = 14,
        SendLock = 15,
        SendItemDatabaseData = 16,
        SetIconState = 17,
        ItemEffect = 18,
        SetCharacterState = 19,
        PingReply = 20,
        PingReq = 21,
        GotPunched = 22,
        AppCheckResponse = 23,
        AppIntegrityFail = 24,
        Disconnect = 25,
        BattleJoin = 26,
        BattleEvent = 27,
        UseDoor = 28,
        SendParental = 29,
        GoneFishin = 30,
        Steam = 31,
        PetState = 32
    }
}
