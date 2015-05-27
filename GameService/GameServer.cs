using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Threading;

namespace GameService
{
    //  класс который представляет игровой сервер
    [ServiceBehavior(InstanceContextMode=InstanceContextMode.Single, ConcurrencyMode=ConcurrencyMode.Multiple)]
    public class GameServer : IGameServer
    {
        //список комнат
        List<Room> rooms=new List<Room>();
        //список пользователей
        List<User> users=new List<User>();
        //связь с бд - ентити
        BombsEntities entity = new BombsEntities();
        //объект для синхронизации потоков
        Mutex mutex = new Mutex();
        //вход
        public void Login(string name, string pass)
        {
            try
            {
                var res = entity.Users.First(x => x.logins == name && x.pass == pass);
                ICallBackGameServer client = OperationContext.Current.GetCallbackChannel<ICallBackGameServer>();
                (client as IContextChannel).Closed += GameServer_Closed;
                client.LoginSuccess();
                mutex.WaitOne();
                try
                {
                    users.Add(new User() { name = name, client = OperationContext.Current.GetCallbackChannel<ICallBackGameServer>(), id = res.id });
                    foreach (var item in users)
                        item.client.UpdateUsersCount(users.Count);
                }
                catch (Exception) {}
                finally { mutex.ReleaseMutex(); }
                client.UpdateRooms(rooms.Select(p => p.roomName).ToList());
            }
            catch (Exception ex) { OperationContext.Current.GetCallbackChannel<ICallBackGameServer>().RecieveMessage("login failed"); }
        }

        //событие закрытия объекта связи
        void GameServer_Closed(object sender, EventArgs e)
        {
            mutex.WaitOne();
            try
            {
                users.Remove(users.First(x => x.client == (ICallBackGameServer)sender));
                foreach (var item in users)
                    item.client.UpdateUsersCount(users.Count);
            }
            catch (Exception ex)
            {
                foreach (var item in this.rooms)
                    item.CheckIfMineUserDisconnect((ICallBackGameServer)sender);
            }
            finally
            {
                mutex.ReleaseMutex(); 
            }
        }

        //регистрация
        public void Registration(string name, string pass)
        {
            Users user = new Users() { logins = name, pass = pass };
            try
            {
                entity.Users.Add(user);
                entity.SaveChanges();
                OperationContext.Current.GetCallbackChannel<ICallBackGameServer>().RecieveMessage("Registration successful");
            }
            catch (Exception ex) 
            { 
                entity.Users.Remove(user); 
                OperationContext.Current.GetCallbackChannel<ICallBackGameServer>().RecieveMessage("Registration failed"); 
            }
        }

        //вход в комнату
        public void EnterRoom(string roomName, string pass = null)
        {
            Room room = rooms.FirstOrDefault(r => r.roomName == roomName);
            if (room != null)
            {
                User user = users.FirstOrDefault(u => u.client == OperationContext.Current.GetCallbackChannel<ICallBackGameServer>());
                if (user != null)
                {
                    if (room.EnterRoom(user, pass))
                    {
                        mutex.WaitOne();
                        try
                        {
                            this.users.Remove(user);
                            foreach (var item in this.users)
                                item.client.UpdateUsersCount(this.users.Count);
                        }
                        catch (Exception)
                        {
                        }
                        finally
                        {
                            mutex.ReleaseMutex();
                        }
                    }
                    else
                        user.client.RecieveMessage("Entrance failed, room filled or wrong password");
                }
                else
                    user.client.RecieveMessage("Entrance failed, room does not exist");
            }
        }

        //Вызывается от клиента при нажатии клавиши
        public void MakeMove(int KeyCode, string roomName, int id)
        {
            //Находим  нашу комнату и вызывам метод 
            for (int i = 0; i < rooms.Count; i++)
            {
                if (roomName == rooms[i].roomName)
                {
                    rooms[i].MakeMove(KeyCode, id);
                    break;
                }
            }
        }

        //выход из комнаты
        public void LeaveRoom(string roomName)
        {
            Room room = rooms.FirstOrDefault(r => r.roomName == roomName);
            if (room != null)
            {
                List<User> usersLeft = room.LeaveRoom();
                usersLeft.ForEach(x => { x.client.RoomLeaveSuccessful(); x.client.IsAdmin(false); });
                foreach (var user in usersLeft)
                {
                    mutex.WaitOne();
                    try
                    {
                        //возвращаем исходное окно/список комнат
                        user.client.LoginSuccess();
                        this.users.Add(user);
                        
                    }
                    catch (Exception)
                    {
                    }
                    finally
                    {
                        mutex.ReleaseMutex();
                    }
                }
                mutex.WaitOne();
                foreach (var item in this.users)
                {
                    item.client.UpdateUsersCount(this.users.Count);
                    item.client.UpdateRooms(rooms.Select(r => r.roomName).ToList());
                }
                mutex.ReleaseMutex();
            }
            else
                OperationContext.Current.GetCallbackChannel<ICallBackGameServer>().RecieveMessage("Error, room not found");
        }

        //создание комнаты
        public void CreateRoom(string roomName, string pass = null)
        {
            if (!rooms.Any(r => r.roomName == roomName))
            {
                User user = users.FirstOrDefault(u => u.client == OperationContext.Current.GetCallbackChannel<ICallBackGameServer>());
                if (user != null)
                {
                    Room room = new Room(user, roomName, pass);
                    room.roomClosed = this.RoomClosed;
                    user.client.IsAdmin(true);
                    mutex.WaitOne();
                    this.rooms.Add(room);
                    try
                    {
                        this.users.Remove(user);
                        foreach (var item in this.users)
                        {
                            item.client.UpdateUsersCount(this.users.Count);
                            item.client.UpdateRooms(this.rooms.Select(r => r.roomName).ToList());
                        }
                    }
                    catch (Exception)
                    {
                    }
                    finally
                    {
                        mutex.ReleaseMutex();
                    }
                }
            }
            else
                OperationContext.Current.GetCallbackChannel<ICallBackGameServer>().RecieveMessage("Room already exists");
        }

        //закрытие комнаты
        private void RoomClosed(Room room)
        {
            mutex.WaitOne();
            this.rooms.Remove(room);
            mutex.ReleaseMutex();
        }

        //начало игры
        public void StartGame(string roomName)
        {
            Room room = rooms.FirstOrDefault(r => r.roomName == roomName);
            if (room != null)
                room.StartGame();
        }
    }
}
