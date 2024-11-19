using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cove.GodotFormat;

namespace Cove.Server.Actor
{
    public class RainCloud : WFActor
    {

        public Vector3 toCenter;
        public float wanderDirection;

        public bool isStaic = false;

        public RainCloud(int ID, Vector3 entPos) : base(ID, "raincloud", Vector3.zero)
        {
            pos = entPos;

            toCenter = (pos - new Vector3(30, 40, -50)).Normalized();
            wanderDirection = new Vector2(toCenter.x, toCenter.z).Angle();
            despawn = true;
            despawnTime = 540;
        }

        public override void onUpdate()
        {
            if (isStaic) return; // for rain that dont move

            Vector2 dir = new Vector2(-1, 0).Rotate(wanderDirection) * (0.17f / 6f);
            pos += new Vector3(dir.x, 0, dir.y);
        }
    }
}
