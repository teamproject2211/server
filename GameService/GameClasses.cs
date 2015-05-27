using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using System.Drawing;

namespace GameService
{
    //класс который реализует конкретную комнату
    public class Room
    {
        //карта для передачи клиенту
        internal byte[][] field;
        //список пользователей
        internal List<User> users = new List<User>();
        //имя комнаты
        public string roomName;
        //ид админа
        int owner;
        //пароль комнаты
        string pass;
        //происодит ли игра
        bool gameStarted = false;
        //событие закрытия комнаты
        public Action<Room> roomClosed = null;
        //дист игровых объектов
        internal List<GameObject> objects = new List<GameObject>();
        //объект для синхронизации многопоточности
        Mutex mutex = new Mutex();
        //конструктор
        public Room(User owner, string roomName, string pass = null)
        {
            this.roomName = roomName;
            this.pass = pass;
            this.owner = owner.id;
            EnterRoom(owner, pass);
            this.field = MapLoader();
        }

        //считывание карты и создание объектов
        byte[][] MapLoader()
        {
            Random r = new Random();

            string fileName = @"Maps\" + r.Next(1, 11).ToString() + ".txt";
            byte[][] print = new byte[15][];
            string[] data;
            string line = "";
            System.IO.StreamReader file = null;

            file = new System.IO.StreamReader(fileName);
            int count = 0;
            while ((line = file.ReadLine()) != null)
            {
                data = line.Split(';');

                print[count] = new byte[20];
                for (int j = 0; j < 20; j++)
                {
                    print[count][j] = byte.Parse(data[j]);
                }
                count++;

            }
            file.Close();
            return print;

        }

        //вход пользователя в комнату
        public bool EnterRoom(User user, string pass = null)
        {
            if (pass == this.pass && !this.gameStarted)
            {
                if (users.Count < 4)
                {
                    mutex.WaitOne();
                    users.Add(user);
                    user.client.RoomEnterSuccessful();
                    mutex.ReleaseMutex();
                    this.UpdatePlayersList();
                    return true;
                }
                else
                    return false;
            }
            else
                return false;
        }

        //выход пользователя/пользователей с комнаты
        public List<User> LeaveRoom()
        {
            User user = this.users.FirstOrDefault(u =>
                u.client == OperationContext.Current.GetCallbackChannel<ICallBackGameServer>());
            if (user != null)
            {
                if (user.id != owner)
                {                mutex.WaitOne();
                users.Remove(user);
                mutex.ReleaseMutex();

                    this.UpdatePlayersList();
                    return new List<User>() { user };
                }
                else
                {
                    if (this.roomClosed != null)
                        this.roomClosed(this);
                    return users;
                }
            }
            else return new List<User>();
        }

        //обновление картыу клиентов
        public void UpdateMap()
        {
            try
            {
                this.mutex.WaitOne();
                this.users.ForEach(x => x.client.UpdateMap(this.field));
            }
            catch (Exception ex) { }
            finally { this.mutex.ReleaseMutex(); }
        }

        //конец игры
        void FinishGame()
        {

        }

        //нажатие игровой клавиши пользователем
        internal void MakeMove(int KeyCode,int id)
        {
            try
            {
                Player player = this.objects.First(x => x is Player && x.id == id) as Player;
                if (player.Move(KeyCode))
                    UpdateMap();
            }
            catch (Exception ex) { }
        }

        //поиск и удаление пользователя в комнате при экстренном отключении 
        public void CheckIfMineUserDisconnect(ICallBackGameServer sender)
        {
            User user = this.users.FirstOrDefault(u =>
                u.client == sender);
            if (user != null)
            {
                mutex.WaitOne();
                users.Remove(user);
                mutex.ReleaseMutex();
                if (!this.gameStarted)
                    this.UpdatePlayersList();
                else
                    this.UserDisappear(user);
            }
        }

        //начало игры
        public void StartGame()
        {
            try
            {
                this.mutex.WaitOne();
                this.users.ForEach(x => x.client.GameStartSuccess());
            }
            catch (Exception ex) { }
            finally { this.mutex.ReleaseMutex(); }

            UpdateMap();
        }

        //отключение пользователя во время игры
        private void UserDisappear(User user)
        {
            throw new NotImplementedException();
        }

        //обновление списка игроков
        private void UpdatePlayersList()
        {
            if (!this.gameStarted)
            {
                mutex.WaitOne();
                foreach (var item in this.users)
                    item.client.UpdatePlayersList(users.Select(u => u.name).ToList());
                mutex.ReleaseMutex();
            }
        }

        //взрыв бомбы
        public void ExplosionBombs(List<Point> points)
        {
            List<Point> correctp = points.Where(t => t.X >= 0 && t.X < field.GetLength(0) &&
                t.Y >= 0 && t.Y < field.GetLength(1)).ToList();

            for (int i = 0; i < objects.Count; i++)
            {
                for (int j = 0; j < correctp.Count; j++)
                {
                    if (objects[i].IsMap(correctp[j].X, correctp[j].Y))
                    {
                        objects[i].Explosion();
                        correctp.RemoveAt(j);
                    }
                }
            }

            Fire f;
            for (int i = 0; i < correctp.Count; i++)
            {
                f = new Fire(this, correctp[i].X, correctp[i].Y);
                objects.Add(f);
            }

        }
    }

    //класс который представляет пользователя
    public class User
    {
        //callback contract
        public ICallBackGameServer client { get; set; }
        //имя и id пользователя
        public int id { get; set; }
        public string name { get; set; }
    }

    //класс который предсталяет игрока на карте
    internal class Player : GameObject
    {
        //хп игрока
        int hp;

        //конструктор
        public Player(Room r, int x, int y)
            : base(r, x, y)
        {
            hp = 0;
            // key = 7;
            myRoom.field[x][y] = key;
        }

        //удаление игрока с карты
        public override void Explosion()
        {
            //уничтожили себя на карте
            (myRoom.users.FirstOrDefault<User>(u => u.id == id)).client.RecieveMessage("Вас убили");
            myRoom.field[x][y] = 0;
            this.myRoom.objects.Remove(this);
        }

        //передвижение игрока/установка бомбы
        public bool Move(int keyCode)
        {
            switch (keyCode)
            {
                case (1):
                    if (x - 1 >= 0 && myRoom.field[x - 1][y] == 0)
                    {
                        myRoom.field[x][y] = 0;
                        myRoom.field[--x][y] = key;
                        return true;
                    }
                    break;
                case (2):
                    if (x + 1 < myRoom.field.GetLength(0) && myRoom.field[x + 1][y] == 0)
                    {
                        myRoom.field[x][y] = 0;
                        myRoom.field[++x][y] = key;
                        return true;
                    }
                    break;
                case (3):
                    if (y - 1 >= 0 && myRoom.field[x][y - 1] == 0)
                    {
                        myRoom.field[x][y] = 0;
                        myRoom.field[x][--y] = key;
                        return true;
                    }
                    break;
                case (4):
                    if (y + 1 < myRoom.field.GetLength(1) && myRoom.field[x][y + 1] == 0)
                    {
                        myRoom.field[x][y] = 0;
                        myRoom.field[x][++y] = key;
                        return true;
                    }
                    break;

                case (5):
                    {
                        myRoom.objects.Add(new Bomb(myRoom, x, y));
                        return true;
                    }
                    break;
            }
            return false;

        }
    }

    //класс который представляет бомбу
    internal class Bomb : GameObject
    {
        //таймер взрыва
        System.Timers.Timer timer;

        //конструктор
        public Bomb(Room r, int x, int y)
            : base(r, x, y)
        {
            key = 3;
            myRoom.field[x][y] = key; 

            timer = new System.Timers.Timer();
            timer.Elapsed += Explode;
            timer.Interval = 2000;
            timer.Start();
        }


        //уничтожение бомбы
        public override void Explosion()
        {
            //уничтожили себя на карте
            myRoom.field[x][y] = 0;
            this.myRoom.objects.Remove(this);

            Fire f = new Fire(myRoom, x, y);
            myRoom.objects.Add(f);
        }

        //событие tick таймера - взрыв
        public void Explode(object sender, System.Timers.ElapsedEventArgs e)
        {
            timer.Stop();

            List<Point> points = new List<Point>();

            points.Add(new Point(x - 1, y));

            points.Add(new Point(x + 1, y));

            points.Add(new Point(x, y - 1));

            points.Add(new Point(x, y + 1));

            points.Add(new Point(x, y));

            myRoom.ExplosionBombs(points);

        }

    }

    //класс который представляет неразрушимый блок
    internal class Block : GameObject
    {

        //конструктор
        public Block(Room r, int x, int y)
            : base(r, x, y)
        {
            key = 1;
            myRoom.field[x][y] = key;
        }

        //уничтожение блока
        public override void Explosion()
        {
            //блок невозможно уничтожить
        }

    }

    //класс который представляет кирпичную стенку
    internal class Brick : GameObject
    {

        //конструктор
        public Brick(Room r, int x, int y)
            : base(r, x, y)
        {
            key = 2;
            myRoom.field[x][y] = key; 
        }

        //разрушение стенки
        public override void Explosion()
        {
            //уничтожили себя на карте
            myRoom.field[x][y] = 0;
            this.myRoom.objects.Remove(this);

            Fire f = new Fire(myRoom, x, y);
            myRoom.objects.Add(f);
        }

    }

    //класс который представляет огонь
    internal class Fire : GameObject
    {
        //таймер горения
        System.Timers.Timer timer;

        //конструктор
        public Fire(Room r, int x, int y)
            : base(r, x, y)
        {
            key = 4;
            myRoom.field[x][y] = key; 

            timer = new System.Timers.Timer();
            timer.Elapsed += timer_Elapsed;
            timer.Interval = 2000;
            timer.Start();
        }

        //timer tick когда огонь догорел
        void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Explosion();
        }

        //уничтожение огня
        public override void Explosion()
        {
            //уничтожили себя на карте
            myRoom.field[x][y] = 0;
            this.myRoom.objects.Remove(this);
        }

    }

}
