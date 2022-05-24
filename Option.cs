using System;

namespace DvMod.SteamCutoff
{
    public abstract class Option<T>
        where T : class
    {
        public static Option<T> Of(T? elem)
        {
            return elem == null ? (Option<T>)new None<T>() : new Some<T>(elem);
        }

        public abstract T? ToNullable();
        public abstract Option<U> Map<U>(Func<T, U> f) where U : class;
        public abstract OptionS<U> MapS<U>(Func<T, U> f) where U : struct;
        public abstract Option<U> FlatMap<U>(Func<T, Option<U>> f) where U : class;
        public abstract OptionS<U> FlatMapS<U>(Func<T, OptionS<U>> f) where U : struct;
    }

    public abstract class OptionS<T>
        where T : struct
    {
        public static OptionS<T> Of(T? elem)
        {
            return elem == null ? (OptionS<T>)new NoneS<T>() : new SomeS<T>((T)elem);
        }

        public abstract T? ToNullable();
        public abstract Option<U> Map<U>(Func<T, U> f) where U : class;
        public abstract OptionS<U> MapS<U>(Func<T, U> f) where U : struct;
        public abstract Option<U> FlatMap<U>(Func<T, Option<U>> f) where U : class;
        public abstract OptionS<U> FlatMapS<U>(Func<T, OptionS<U>> f) where U : struct;
    }

    public sealed class None<T> : Option<T>
        where T : class
    {
        public None()
        {
        }

        public override T? ToNullable() => null;
        public override Option<U> Map<U>(Func<T, U> f) where U : class => new None<U>();
        public override OptionS<U> MapS<U>(Func<T, U> f) where U : struct => new NoneS<U>();
        public override Option<U> FlatMap<U>(Func<T, Option<U>> f) where U : class => new None<U>();
        public override OptionS<U> FlatMapS<U>(Func<T, OptionS<U>> f) where U : struct => new NoneS<U>();
    }

    public sealed class NoneS<T> : OptionS<T>
        where T : struct
    {
        public NoneS()
        {
        }

        public override T? ToNullable() => null;
        public override Option<U> Map<U>(Func<T, U> f) where U : class => new None<U>();
        public override OptionS<U> MapS<U>(Func<T, U> f) where U : struct => new NoneS<U>();
        public override Option<U> FlatMap<U>(Func<T, Option<U>> f) where U : class => new None<U>();
        public override OptionS<U> FlatMapS<U>(Func<T, OptionS<U>> f) where U : struct => new NoneS<U>();
    }

    public class Some<T> : Option<T>
        where T : class
    {
        private readonly T elem;
        internal Some(T elem)
        {
            this.elem = elem;
        }
        public override T? ToNullable() => elem;
        public override Option<U> Map<U>(Func<T, U> f) where U : class => new Some<U>(f(elem));
        public override OptionS<U> MapS<U>(Func<T, U> f) where U : struct => new SomeS<U>(f(elem));
        public override Option<U> FlatMap<U>(Func<T, Option<U>> f) where U : class => f(elem);
        public override OptionS<U> FlatMapS<U>(Func<T, OptionS<U>> f) where U : struct => f(elem);
    }

    public class SomeS<T> : OptionS<T>
        where T : struct
    {
        private readonly T elem;
        internal SomeS(T elem)
        {
            this.elem = elem;
        }
        public override T? ToNullable() => (T?)elem;
        public override Option<U> Map<U>(Func<T, U> f) where U : class => new Some<U>(f(elem));
        public override OptionS<U> MapS<U>(Func<T, U> f) where U : struct => new SomeS<U>(f(elem));
        public override Option<U> FlatMap<U>(Func<T, Option<U>> f) where U : class => f(elem);
        public override OptionS<U> FlatMapS<U>(Func<T, OptionS<U>> f) where U : struct => f(elem);
    }
}
