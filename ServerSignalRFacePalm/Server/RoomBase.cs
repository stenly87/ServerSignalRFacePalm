
using Microsoft.AspNetCore.SignalR;
using System;

namespace ServerSignalRFacePalm.Server
{
    public class RoomBase
    {
        Dictionary<string, Room> rooms = new();
        Dictionary<string, Queue<RoomAction>> defenceActions = new();
        Dictionary<string, Queue<RoomAction>> attackActions = new();

        Room last;
        internal string AddNewPlayerAndGetRoom(Player player)
        {
            string result = string.Empty;
            if (last == null)
            {
                last = new Room { Number = Guid.NewGuid().ToString().ToLower() };
                rooms.Add(last.Number, last);                
                defenceActions.Add(last.Number, new());
                attackActions.Add(last.Number, new());
            }
            result = last.Number;
            last.PlayerState.Add(player.Name, player);
            if (last.PlayerState.Count >= 2)
                last = null;

            return result;
        }

        internal async Task AddRoundActionAsync(RoomAction action, IHubCallerClients clients)
        {
            if (!rooms.TryGetValue(groupId, out Room room))
            {
                Console.WriteLine($"Команты {groupId} не существует");
                return;
            }
            if (!room.PlayerState.ContainsKey(action.Actor))
            {
                Console.WriteLine($"Игрока {action.Actor} не существует");
                return;
            }
            if (room.PlayerState[action.Actor].HP <= 0)
                return;
            
            var alreadyDefence = defenceActions[action.GroupId].Any(s => s.Actor == action.Actor);
            var alreadyAttack = attackActions[action.GroupId].Any(s => s.Actor == action.Actor);

            if (alreadyAttack || alreadyDefence)
                return;
            
            if (action.ActionType == 1)
                attackActions[action.GroupId].Enqueue(action);
            else
                defenceActions[action.GroupId].Enqueue(action);

            int count = attackActions[action.GroupId].Count + defenceActions[action.GroupId].Count;
            if (count == room.PlayerState.Count(s=>s.Value.HP > 0))
            {
                Console.WriteLine("Месилово!");
                await PlayRoundAsync(action.GroupId, clients);
            }
        }

        private async Task PlayRoundAsync(string groupId, IHubCallerClients clients)
        {
            List<string> actions = new List<string>();
            Random random = new();
            Dictionary<string, int> defence = new();
            while (defenceActions[groupId].Count > 0)
            {
                var action = defenceActions[groupId].Dequeue();
                defence.Add(action.Target, random.Next(2, 6));
                actions.Add($"Игрок {action.Actor} защищает {action.Target} . Очков защиты: {defence[action.Target]}");
            }
            while (attackActions[groupId].Count > 0)
            {
                var damage = random.Next(1, 4);
                var action = attackActions[groupId].Dequeue();
                actions.Add($"Игрок {action.Actor} жестоко избивает игрока {action.Target}. Начальная атака: {damage}");
                if (defence.ContainsKey(action.Target))
                {
                    if (defence[action.Target] > damage)
                    {
                        defence[action.Target] -= damage;
                        damage = 0;
                        actions.Add($"Игрок {action.Target} успешно защищается от атаки");
                    }
                    else
                    {
                        damage -= defence[action.Target];
                        defence[action.Target] = 0;
                        actions.Add($"Игрок {action.Target} частично защищается от атаки");
                    }
                    if (defence[action.Target] == 0)
                        defence.Remove(action.Target);
                }
                rooms[groupId].PlayerState[action.Target].HP -= damage;
                if (damage > 0)
                    actions.Add($"Игрок {action.Target} страдает от урона {damage}. Итоговое хп: {rooms[groupId].PlayerState[action.Target].HP}");
                if (rooms[groupId].PlayerState[action.Target].HP <= 0)
                    actions.Add($"Игрок {action.Target} не выдерживает и умирает");
            }
            await clients.Group(groupId).SendAsync("PastRoundInfo", actions);
            if (rooms[groupId].PlayerState.Count(s => s.Value.HP > 0) > 1)
                await clients.Group(groupId).SendAsync("StartRound", rooms[groupId]);
            else
            {
                await clients.Group(groupId).SendAsync("Winner", rooms[groupId].PlayerState.FirstOrDefault(s => s.Value.HP > 0).Value);
                rooms.Remove(groupId);
            }
        }

        internal bool Ready(string groupId)
        {
            if (!rooms.TryGetValue(groupId, out Room room))
                return false;
            return room.PlayerState.Count >= 2;
        }

        internal async Task StartAsync(string groupId, IHubCallerClients clients)
        {
            if (!rooms.TryGetValue(groupId, out Room room))
                return;
            // пользователь в методе-обработчике команды StartRound должен выбрать действие
            await clients.Group(groupId).SendAsync("StartRound", room);
        }
    }
}
