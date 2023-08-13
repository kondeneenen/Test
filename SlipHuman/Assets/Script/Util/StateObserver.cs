using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace Util
{
    //--------------------------------------------------------------------------------
    /// <summary>
    /// ステート管理基底
    /// </summary>
    public class StateObserverBase
    {
        public StateObserverBase() { mStateTime = VfrSecondCounter.Zero; }

        public virtual void SetState(int index)
        {
            mPrevIndex = mCurIndex;
            mCurIndex = index;
            mChangeFlag = true;
            mStateCounter = 0;
            ResetStateTime();
        }

        public float MaxStateNum { get { return mMaxStateNum; } }
        public int CurState { get { return mCurIndex; } }
        public int PrevState { get { return mPrevIndex; } }
        public bool IsStateChanging { get { return mChangeFlag; } }
        public int StateCount { get { return mStateCounter; } set { mStateCounter = value; } }
        public float StateTime { get { return mStateTime.Value; } }

        public void SetStateDirect(int state) { mCurIndex = state; }
        public void UpdateStateTime() { mStateTime.Update(); }
        public virtual void ResetState()
        {
            mCurIndex = 0;
            mPrevIndex = 0;
            mChangeFlag = true;
            mStateCounter = 0;
            ResetStateTime();
        }
        public bool CheckOverStateTimeInThisFrame(float sec) { return mStateTime.CheckOverInThisFrame(sec); }
        public void ResetStateTime(float time = 0f) { mStateTime.SetValue(time); }

        //--------------------------------------------------------------------------------
        // field
        //--------------------------------------------------------------------------------
        protected int mMaxStateNum = 0;
        protected int mCurIndex = 0;
        protected int mPrevIndex = 0;
        protected bool mChangeFlag = true;
        protected VfrSecondCounter mStateTime;
        protected int mStateCounter = 0;
    }

    public class StateObserver : StateObserverBase
    {
        public StateObserver(int maxStateNum) : base()
        {
            mMaxStateNum = maxStateNum;
            mInitFuncList = new System.Action[mMaxStateNum];
            mDoFuncList = new System.Action[mMaxStateNum];
            mExitFuncList = new System.Action[mMaxStateNum];
            ResetState();
        }

        public void RegisterState(System.Action doFunc, int id)
        {
            mInitFuncList[id] = null;
            mDoFuncList[id] = doFunc;
            mExitFuncList[id] = null;
        }

        public void RegisterState(System.Action initFunc, System.Action doFunc, int id)
        {
            mInitFuncList[id] = initFunc;
            mDoFuncList[id] = doFunc;
            mExitFuncList[id] = null;
        }

        public void RegisterState(System.Action initFunc, System.Action doFunc, System.Action exitFunc, int id)
        {
            mInitFuncList[id] = initFunc;
            mDoFuncList[id] = doFunc;
            mExitFuncList[id] = exitFunc;
        }

        public virtual void ExecuteState()
        {
            if (mChangeFlag)
            {
                exitStateFunc(mPrevIndex);
                mChangeFlag = false;
                initStateFunc();
            }
            doStateFunc();
            mStateCounter++;
            mStateTime.Update();
        }

        protected void initStateFunc()
        {
            if (mInitFuncList[mCurIndex] != null)
            {
                mInitFuncList[mCurIndex]();
            }
        }

        protected void doStateFunc()
        {
            if (mDoFuncList[mCurIndex] != null)
            {
                mDoFuncList[mCurIndex]();
            }
        }

        protected void exitStateFunc(int index)
        {
            if (mExitFuncList[index] != null)
            {
                mExitFuncList[index]();
            }
        }

        protected System.Action[] mInitFuncList = null; // 初期化関数リスト
        protected System.Action[] mDoFuncList = null; // 更新関数リスト
        protected System.Action[] mExitFuncList = null; // 終了関数リスト
    }

    //--------------------------------------------------------------------------------
    /// <summary>
    /// ステート遷移がリクエスト制の StateObserver
    /// ExecuteState後にステートが切り替わる
    /// </summary>
    public sealed class StateObserverEx : StateObserver
    {
        public StateObserverEx(int maxStateNum) : base(maxStateNum) { }

        /// <summary>
        /// ステート遷移リクエスト
        /// </summary>
        public void RequestChangeState(int index)
        {
            Assert.IsTrue(index < mMaxStateNum);
            mNextIndex = index;
            mChangeFlag = true;
        }

        /// <summary>
        /// 遷移リクエストが存在していない場合にのみステート遷移リクエスト
        /// </summary>
        public bool RequestChangeStateIfNotChange(int index)
        {
            Assert.IsTrue(index < mMaxStateNum);
            if (IsStateChanging)
            {
                return false;
            }
            else
            {
                RequestChangeState(index);
                return true;
            }
        }

        public int NextState { get { return mNextIndex; } }

        /// <summary>
        /// 現在がそのステートである、もしくは、次にそのステートになるかどうか
        /// </summary>
        public bool IsCurOrNext(int index) { return (index == mCurIndex || (index == NextState && IsStateChanging)); }

        /// <summary>
        /// 現在がそのステートである、かつ、次もそのステートを維持するかどうか
        /// </summary>
        public bool IsCurAndNext(int index) { return (index == mCurIndex && (index == NextState && IsStateChanging)); }

        /// <summary>
        /// リクエスト制ではなく、即座にステート変更
        /// </summary>
        public override void SetState(int index)
        {
            base.SetState(index);
            mNextIndex = index;
            mChangeFlag = false; // true の間違い？
        }

        /// <summary>
        /// リクエスト制ではなく、即座にステート初期化
        /// </summary>
        public override void ResetState()
        {
            base.ResetState();
            mNextIndex = 0;
        }

        /// <summary>
        /// ステート実行
        /// </summary>
        public override void ExecuteState()
        {
            if (mChangeFlag)
            {
                exitStateFunc(mCurIndex);
                mPrevIndex = mCurIndex;
                mCurIndex = mNextIndex;
                mStateCounter = 0;
                ResetStateTime();
                mChangeFlag = false;
                initStateFunc();
            }
            doStateFunc();
            mStateCounter++;
            mStateTime.Update();
        }

        private int mNextIndex = 0;
    }
}
