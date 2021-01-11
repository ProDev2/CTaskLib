/*
 * Copyright (c) 2021 GVoid (Pascal Gerner)
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace System.Collections.Generic
{
    public class Deque<T> : IEnumerable {
        public const float NO_GROWTH = 0.5f;
        public const float NO_EXTRA_GROWTH = 1f;

        private T[] mElem;
        private int mOff, mLen, mCap;

        public Deque(int capacity) {
            mElem = null;
            mOff = 0; mLen = 0; mCap = 0;
            Resize(capacity, false);
        }
        
        public virtual bool Ensure(int needed, float growth = 1.75f) {
            float g = growth;
            needed = Math.Max(needed, 0);
            int len = mLen, cap = mCap;
            cap += needed -= cap - len;
            if (needed <= 0) return true;
            if (cap < 0) return false;
            if (g > 0f & g < 1f) return false;
            int nCap = cap + (int) Math.Max(
                g > 0f ? cap * g : -g, 0f);
            if (nCap > cap) cap = nCap;
            return Resize(cap, true);
        }

        public virtual bool Resize(int capacity, bool lossless = false) {
            capacity = Math.Max(capacity, 0);
            T[] nextElem = new T[capacity];
            int len = Math.Min(mLen, capacity);
            if (mElem != null && mLen > 0) {
                if (lossless && len < mLen) return false;
                int len1 = Math.Min(mCap - mOff, len);
                int len2 = len - len1;
                if (len1 > 0) Array.Copy(mElem, mOff, nextElem, 0, len1);
                if (len2 > 0) Array.Copy(mElem, 0, nextElem, len1, len2);
            }
            mElem = nextElem;
            mOff = 0; mLen = len; mCap = capacity;
            return true;
        }

        public void Clear() {
            Clean(0, -1);
            ClearLazy();
        }

        public virtual void ClearLazy() {
            mLen = 0;
        }

        public void CleanRange(int from, int to) {
            int dist;
            if ((from | (mLen - to) | (dist = to - from)) < 0) {
                throw new IndexOutOfRangeException();
            }
            Clean(from, dist);
        }

        public virtual int Clean(int from, int len) {
            if (mCap <= 0 || from < 0) return 0;
            int maxLen = mLen - from;
            if (len >= 0) maxLen = Math.Min(maxLen, len);
            for (int i = 0; i < maxLen; i++) {
                int fi = ins(mOff + from + i, mCap);
                mElem[fi] = default;
            }
            return Math.Max(maxLen, 0);
        }

        public int Capacity {
            get => Math.Max(mCap, 0);
            set => Resize(value, false);
        }

        public int Size {
            get => Math.Max(Math.Min(mLen, mCap), 0);
        }

        public bool Empty {
            get => mLen <= 0;
        }

        public bool AtMax {
            get => mLen >= mCap;
        }

        public bool OfferFirst(T e) {
            if (mCap <= 0) return false;
            mOff = ins(mOff - 1, mCap);
            mElem[mOff] = e;
            mLen = Math.Min(mLen + 1, mCap);
            return true;
        }

        public bool OfferLast(T e) {
            if (mCap <= 0) return false;
            int i = ins(mOff + mLen, mCap);
            mElem[i] = e;
            mOff = ins(mOff + Math.Max(++mLen - mCap, 0), mCap);
            mLen = Math.Min(mLen, mCap);
            return true;
        }

        public T PushFirst(T e) {
            if (mCap <= 0) return e;
            mOff = ins(mOff - 1, mCap);
            T prevElem = mLen >= mCap ? mElem[mOff] : default;
            mElem[mOff] = e;
            mLen = Math.Min(mLen + 1, mCap);
            return prevElem;
        }

        public T PushLast(T e) {
            if (mCap <= 0) return e;
            int i = ins(mOff + mLen, mCap);
            T prevElem = mLen >= mCap ? mElem[i] : default;
            mElem[i] = e;
            mOff = ins(mOff + Math.Max(++mLen - mCap, 0), mCap);
            mLen = Math.Min(mLen, mCap);
            return prevElem;
        }

        public T PollFirst() {
            if (mCap <= 0) return default;
            T elem = mLen > 0 ? mElem[mOff] : default;
            mElem[mOff] = default;
            mOff = ins(mOff + 1, mCap);
            mLen = Math.Max(mLen - 1, 0);
            return elem;
        }

        public T PollLast() {
            if (mCap <= 0) return default;
            int i = ins(mOff + mLen - 1, mCap);
            T elem = mLen > 0 ? mElem[i] : default;
            mElem[i] = default;
            mLen = Math.Max(mLen - 1, 0);
            return elem;
        }

        public T PeekFirst() {
            if (mCap <= 0) return default;
            return mLen > 0
                    ? mElem[mOff]
                    : default;
        }

        public T PeekLast() {
            if (mCap <= 0) return default;
            return mLen > 0
                    ? mElem[ins(mOff + mLen - 1, mCap)]
                    : default;
        }

        public T GetAt(int index) {
            if (index < 0 || index >= mLen) {
                throw new IndexOutOfRangeException();
            }
            return mElem[ins(mOff + index, mCap)];
        }

        public T SetAt(int index, T elem) {
            if (index < 0 || index >= mLen) {
                throw new IndexOutOfRangeException();
            }
            int i = ins(mOff + index, mCap);
            T prevElem = mElem[i];
            mElem[i] = elem;
            return prevElem;
        }

        public int RemoveFirst(int len, bool lazy) {
            if (mCap <= 0) return 0;
            int maxLen = mLen;
            if (len >= 0) maxLen = Math.Min(maxLen, len);
            if (!lazy) Clean(0, maxLen);
            mOff = ins(mOff + maxLen, mCap);
            mLen = Math.Max(mLen - maxLen, 0);
            return Math.Max(maxLen, 0);
        }

        public int RemoveLast(int len, bool lazy) {
            if (mCap <= 0) return 0;
            int maxLen = mLen;
            if (len >= 0) maxLen = Math.Min(maxLen, len);
            if (!lazy) Clean(mLen - maxLen, maxLen);
            mLen = Math.Max(mLen - maxLen, 0);
            return Math.Max(maxLen, 0);
        }

        private static int ins(int n, int c)
            => (n %= c) >= 0 ? n : c + n;
        
        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public IEnumerator GetEnumerator(bool descending = false) {
            return new DequeIterator<T>(this, descending);
        }
        
        public class DequeIterator<E> : IEnumerator {
            protected readonly Deque<E> mDeque;
            protected readonly bool mDescending;

            protected int mCursor;
            protected E mElem;

            public DequeIterator(Deque<E> deque, bool descending) {
                if (deque == null) {
                    throw new NullReferenceException();
                }

                mDeque = deque;
                mDescending = descending;

                mCursor = -1;
                mElem = default;
            }

            public bool MoveNext() {
                mCursor = Math.Max(mCursor, 0);
                mElem = default;

                int len = mDeque.mLen;
                if (mCursor >= len) return false;

                int i = mDescending
                        ? len - 1 - mCursor
                        : mCursor;
                int fi = ins(
                        mDeque.mOff + i,
                        mDeque.mCap
                );
                mCursor += 1;
                mElem = mDeque.mElem[fi];
                return true;
            }

            public void Reset() {
                mCursor = -1;
                mElem = default;
            }

            object IEnumerator.Current {
                get => mElem;
            }

            public E Current {
                get => mElem;
            }
        }
    }
}