using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Threading;

namespace GameService
{
    //интерфейс представляет контракт службы
    [ServiceContract(CallbackContract=typeof(ICallBackGameServer))]
    public interface IGameServer
    {
        //вход
        [OperationContract(IsOneWay=true)]
        void Login(string name, string pass);

        //регистрация
        [OperationContract(IsOneWay=true)]
        void Registration(string name, string pass);

        //вход в комноту
        [OperationContract(IsOneWay=true)]
        void EnterRoom(string roomName, string pass = null);

        //нажатие клавиши пользователем
        [OperationContract(IsOneWay = true)]
        void MakeMove(int KeyCode, string roomName,int id);

        //выход из комнаты
        [OperationContract(IsOneWay = true)]
        void LeaveRoom(string roomName);

        //создание комнаты
        [OperationContract(IsOneWay = true)]
        void CreateRoom(string roomName, string pass = null);

        //начало игры
        [OperationContract(IsOneWay = true)]
        void StartGame(string roomName);
    }


    //интерфейс представляет callback констракт клиента
    public interface ICallBackGameServer
    {
        //обновление карты
        [OperationContract(IsOneWay = true)]
        void UpdateMap(byte[][] map);

        //вывод сообщения
        [OperationContract(IsOneWay = true)]
        void RecieveMessage(string msg);

        //успешный вход
        [OperationContract(IsOneWay = true)]
        void LoginSuccess();

        //обновление кол-ва пользователей
        [OperationContract(IsOneWay = true)]
        void UpdateUsersCount(int count);

        //обновление списка комнат
        [OperationContract(IsOneWay = true)]
        void UpdateRooms(List<string> rooms);

        //успешный вход в комнату
        [OperationContract(IsOneWay = true)]
        void RoomEnterSuccessful();

        //успешный выход из комнаты
        [OperationContract(IsOneWay = true)]
        void RoomLeaveSuccessful();

        //обновляет список пользователей в комнате
        [OperationContract(IsOneWay = true)]
        void UpdatePlayersList(List<string> names);

        //активирует/деактивирует кнопку начала игры
        [OperationContract(IsOneWay = true)]
        void IsAdmin(bool IsEnabled);

        //оповещает клиента о начале игры
        [OperationContract(IsOneWay = true)]
        void GameStartSuccess();
    }

    
}
