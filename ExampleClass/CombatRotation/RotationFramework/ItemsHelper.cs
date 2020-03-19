using System.Threading;
using wManager.Wow.Helpers;

public class ItemsHelper
{
	//returns cooldown in seconds
	public static float GetItemCooldown(string itemName)
	{
		string luaString = $@"
        for bag=0,4 do
            for slot=1,36 do
                local itemLink = GetContainerItemLink(bag,slot);
                if (itemLink) then
                    local itemString = string.match(itemLink, ""item[%-?%d:]+"");
                    if (GetItemInfo(itemString) == ""{itemName}"") then
                        local start, duration, enabled = GetContainerItemCooldown(bag, slot);
                        if enabled == 1 and duration > 0 and start > 0 then
                            return (duration - (GetTime() - start));
                        end
                    end
                end;
            end;
        end
        return 0;";
		return Lua.LuaDoString<float>(luaString);
	}

	public static float GetItemCooldown(uint id)
	{
		return GetItemCooldown(ItemsManager.GetNameById(id));
	}

	public static void DeleteItems(string itemName, int leaveAmount = 0)
	{
		var itemQuantity = ItemsManager.GetItemCountByNameLUA(itemName) - leaveAmount;

		if (string.IsNullOrWhiteSpace(itemName) || itemQuantity <= 0)
		{
			return;
		}

		string luaToDelete = $@"
            local itemCount = {itemQuantity}; 
            local deleted = 0; 
            for b=0,4 do 
                if GetBagName(b) then 
                    for s=1, GetContainerNumSlots(b) do 
                        local itemLink = GetContainerItemLink(b, s) 
                        if itemLink then 
                            local itemString = string.match(itemLink, ""item[%-?%d:]+"");
                            local _, stackCount = GetContainerItemInfo(b, s);
                            local leftItems = itemCount - deleted; 
                            if ((GetItemInfo(itemString) == ""{itemName}"") and leftItems > 0) then 
                                if stackCount <= 1 then 
                                    PickupContainerItem(b, s); 
                                    DeleteCursorItem(); 
                                    deleted = deleted + 1; 
                                else 
                                    if (leftItems > stackCount) then 
                                        SplitContainerItem(b, s, stackCount); 
                                        DeleteCursorItem(); 
                                        deleted = deleted + stackCount; 
                                    else 
                                        SplitContainerItem(b, s, leftItems); 
                                        DeleteCursorItem(); 
                                        deleted = deleted + leftItems; 
                                    end 
                                end
                            end 
                        end 
                    end 
                end 
            end
        ";
		Lua.LuaDoString(luaToDelete);
	}

	public static int GetItemCountSave(uint itemId)
	{
		int itemCount = ItemsManager.GetItemCountById(itemId);
		if (itemCount > 0)
		{
			return itemCount;
		}

		Thread.Sleep(250);
		return ItemsManager.GetItemCountById(itemId);
	}

	public static int GetItemCountSave(string itemName)
	{
		int itemCount = GetItemCount(itemName);
		if (itemCount > 0)
		{
			return itemCount;
		}

		Thread.Sleep(250);
		return GetItemCount(itemName);
	}

	public static int GetItemCount(string itemName)
	{
		string countLua = $@"
        local fullCount = 0;
        for bag=0,4 do
            for slot=1,36 do
                local itemLink = GetContainerItemLink(bag, slot);
                if (itemLink) then
                    local itemString = string.match(itemLink, ""item[%-?%d:]+"");
                    if (GetItemInfo(itemString) == ""{itemName}"") then
                        local texture, count = GetContainerItemInfo(bag, slot);
                        fullCount = fullCount + count;
                    end
                end
            end
        end
        return fullCount;";
		return Lua.LuaDoString<int>(countLua);
	}
}