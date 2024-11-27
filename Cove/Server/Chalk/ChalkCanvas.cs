using Cove.GodotFormat;
using System;

namespace Cove.Server.Chalk
{
    public class ChalkCanvas
    {

        public long canvasID;
        Dictionary<Vector2, int> chalkImage = new Dictionary<Vector2, int>();

        public ChalkCanvas(long canvasID)
        {
            this.canvasID = canvasID;
        }

        public void drawChalk(Vector2 position, int color)
        {
            chalkImage[position] = color;
        }

        public Dictionary<int, object> getChalkPacket()
        {
            Dictionary<int, object> packet = new Dictionary<int, object>();
            ulong i = 0;
            foreach (KeyValuePair<Vector2, int> entry in chalkImage.ToDictionary(pair => pair.Key, pair => pair.Value))
            {
                Dictionary<int, object> arr = new();
                arr[0] = entry.Key;
                arr[1] = entry.Value;
                packet[(int)i] = arr;
                i++;
            }

            return packet;
        }

        public void chalkUpdate(Dictionary<int, object> packet)
        {
            foreach (KeyValuePair<int, object> entry in packet)
            {
                Dictionary<int, object> arr = (Dictionary<int, object>)entry.Value;
                Vector2 vector2 = (Vector2)arr[0];
                Int64 color = (Int64)arr[1];

                chalkImage[vector2] = (int)color;
            }
        }

        public void clearCanvas()
        {
            chalkImage.Clear();
        }
    }
}
