using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DistributedFileStorageCommon
{
    public static class ArrayExtensions
    {
        /// <summary>
        /// DURSTENFELD'S SHUFFLE
        /// </summary>
        /// <typeparam name="t"></typeparam>
        /// <param name="array"></param>
        public static t[] Shuffle<t>(this t[] array)
        {
            //CREATE A TEMPORARY RESULT SET AND CLONE THE ORIGINAL TO IT
            t[] tmpReturn = new t[array.Length];
            array.CopyTo(tmpReturn, 0);

            //CREATE A NEW RANDOM NUMBER GENERATOR USING THE TIME TICKS AS A SEED
            Random rnd = new Random((int)DateTime.Now.Ticks);

            //LOOP THROUGH THE ARRAY BACKWARDS FROM END TO BEGINING
            for (int i = tmpReturn.Length - 1; i > 0; i--)
            {
                //DETERMINE THE NEW LOCATION OF THE ITEM WHICH IS BEING MOVED
                int newIndex = rnd.Next(i + 1);

                //HOLD THE VALUE WHICH WE ARE SWAPPING OVER
                t tmpHolder = tmpReturn[newIndex];

                //SWAP THE TWO INDEXES NOW
                tmpReturn[newIndex] = tmpReturn[i];
                tmpReturn[i] = tmpHolder;
            }

            //RETURN THE RESULT SET TO THE CALLING FUNCTION
            return tmpReturn;
        }
    }
}
