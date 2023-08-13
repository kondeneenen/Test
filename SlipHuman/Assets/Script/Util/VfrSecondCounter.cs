using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Util
{
    //--------------------------------------------------------------------------------
    /// <summary>
    /// 可変フレームレート対応の秒カウンタ
    /// </summary>
    public class VfrSecondCounter
    {
        //--------------------------------------------------------------------------------
        // ctor
        //--------------------------------------------------------------------------------

        public VfrSecondCounter(float counter, EPlusOrMinus plusOrMinus = EPlusOrMinus.Plus)
        {
            mValue = counter;
            mOldSecond = counter;
            mPlusOrMinus = plusOrMinus;
        }

        public VfrSecondCounter(float counter)
        {
            mValue = counter;
            mOldSecond = counter;
            mPlusOrMinus = EPlusOrMinus.Plus;
        }

        //--------------------------------------------------------------------------------
        // define
        //--------------------------------------------------------------------------------

        public enum EPlusOrMinus
        {
            Plus = 0,
            Minus = 1,
        }

        //--------------------------------------------------------------------------------
        // public method
        //--------------------------------------------------------------------------------

        public float Value { get { return mValue; } }
        public static VfrSecondCounter Zero { get { return cZero; } }
        public EPlusOrMinus PlusOrMinus { get { return mPlusOrMinus; } set { mPlusOrMinus = value; } }

        /// <summary>
        /// deltaTime を加算
        /// </summary>
        public void Update()
        {
            mOldSecond = mValue;
            mValue += (Time.deltaTime * plusOrMinus);
        }

        /// <summary>
        /// 値を指定
        /// </summary>
        public void SetValue(float value)
        {
            mValue = value;
            mOldSecond = value;
        }

        /// <summary>
        /// 値に直接加算（可変フレームレートの影響無し）
        /// スイッチを押したから時間が増えた、のようなケースで使用
        /// </summary>
        public void AddDirectValue(float counter)
        {
            mValue += (counter * plusOrMinus);
        }

        public void AddConstCalc(float counter)
        {
            mValue += (Time.deltaTime * counter * plusOrMinus);
        }

        public bool IsLessEqualZero()
        {
            return mValue <= 0f;
        }


        public bool IsLessZero()
        {
            return mValue < 0f;
        }

        public bool IsGreaterEqualZero()
        {
            return mValue >= 0f;
        }


        public bool IsGreaterZero()
        {
            return mValue > 0f;
        }

        public bool ClampMax(float max)
        {
            if (mValue > max)
            {
                mValue = max;
                return true;
            }
            return false;
        }

        public bool ClampMix(float min)
        {
            if (mValue < min)
            {
                mValue = min;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 通過判定
        /// Update後に使用
        /// count が現在カウントと同じが、今フレームで通過した場合に true
        /// </summary>
        public bool CheckOverInThisFrame(float count)
        {
            return (Math.EqualEpsilon(mValue, count) || ((mOldSecond - count) * (mValue - count)) < 0f);
        }

        //--------------------------------------------------------------------------------
        // private method
        //--------------------------------------------------------------------------------

        private float plusOrMinus { get { return mPlusOrMinus == EPlusOrMinus.Plus ? 1f : -1f; } }

        //--------------------------------------------------------------------------------
        // field
        //--------------------------------------------------------------------------------

        private float mValue; // 現在値
        private float mOldSecond; // 前フレーム値（通過判定用）
        private EPlusOrMinus mPlusOrMinus;
        private static readonly VfrSecondCounter cZero = new VfrSecondCounter(0f);
    }
}