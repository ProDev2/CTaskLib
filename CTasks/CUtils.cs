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

using System;
using System.Collections;
using System.Collections.Generic;

namespace CTasks
{
    public static class States {
        public const int STATE_NONE = 0x0;
        public const int STATE_STARTED = 0x1 << 25;
        public const int STATE_READY = STATE_STARTED | 0x1 << 24;
        public const int STATE_DONE = STATE_STARTED | 0x1 << 31;
        public const int STATE_RUNNING = STATE_READY | 0x1 << 26;
        public const int STATE_CANCELED = STATE_DONE | 0x1 << 27;
        public const int STATE_SUCCESS = STATE_DONE | 0x1 << 28;
        public const int STATE_FAILED = STATE_DONE | 0x1 << 29;
        public const int STATE_POST_FAILED = STATE_DONE | 0x1 << 30;

        public static bool IsStarted(int state) {
            return (state & STATE_READY) != 0;
        }

        public static bool IsReady(int state) {
            return (state & STATE_READY) == STATE_READY;
        }

        public static bool IsWaiting(int state) {
            return (state
                    & (STATE_RUNNING | STATE_DONE)
                    & ~STATE_READY
            ) == 0;
        }

        public static bool IsRunning(int state) {
            return (state & STATE_RUNNING & ~STATE_READY) != 0;
        }

        public static bool IsStillRunning(int state) {
            return (state
                    & (STATE_RUNNING | STATE_DONE)
                    & ~STATE_READY
            ) == (STATE_RUNNING & ~STATE_READY);
        }

        public static bool IsDone(int state) {
            return (state & STATE_DONE) == STATE_DONE;
        }

        public static bool IsCanceled(int state) {
            return (state & STATE_CANCELED) == STATE_CANCELED;
        }

        public static bool IsSuccess(int state) {
            return (state & STATE_SUCCESS & ~STATE_DONE) != 0;
        }

        public static bool IsFailed(int state) {
            return (state & STATE_FAILED & ~STATE_DONE) != 0;
        }

        public static bool IsFailedPost(int state) {
            return (state & STATE_POST_FAILED & ~STATE_DONE) != 0;
        }
    }

    public class DStack<T> : IEnumerable {
        private readonly Deque<T> mDeque;
        private readonly float mGrowth;

        public DStack() : this(8, 2f) {}
        public DStack(int capacity, float growth) {
            mDeque = new Deque<T>(capacity);
            mGrowth = growth;
        }

        public int Count {
            get => mDeque.Size;
        }

        public void Clear() {
            mDeque.Clear();
        }

        public void Push(T elem) {
            if (!mDeque.Ensure(1, mGrowth) || !mDeque.OfferFirst(elem)) {
                throw new InvalidOperationException();
            }
        }

        public T Pop() {
            return mDeque.PollLast();
        }

        public T Peek() {
            return mDeque.PeekLast();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return mDeque.GetEnumerator(false);
        }
    }
}