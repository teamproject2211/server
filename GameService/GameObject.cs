using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameService
{
    //абстрактый базовый класс,представляющий игровой объект
    internal abstract class GameObject
    {
        //ссылка на комнату в которой находится данный объект
        protected Room myRoom;

        //координаты объекта
        protected int x, y;

        //личный код объекта
        protected byte key;

        //id игрового объекта
        internal int id;

        //конструктор 
        public GameObject(Room r, int x, int y)
        {
            this.myRoom = r;
            this.x = x;
            this.y = y;
        }

        //асбстрактый мето,который будет вызываться для разрушение объекта
        public abstract void Explosion(); //разрушение объекта

        //стоит ли данной карте объект
        public bool IsMap(int x, int y)
        {
            if (this.x == x && this.y == y)
                return true;

            return false;
        }

    }

}
