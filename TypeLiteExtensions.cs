namespace TypeLitePlus.KO
{
    public static class TypeLiteExtensions
    {
        public static TypeScriptFluentModuleMember AsKoClass(this TypeScriptFluentModuleMember obj)
        {
            TsKnockoutModelGenerator.Tags[obj.Member] = TsKnockoutModelGenerator.KoClass;
            return obj;
        }

        public static TypeScriptFluentModuleMember AsKoInterface(this TypeScriptFluentModuleMember obj)
        {
            TsKnockoutModelGenerator.Tags[obj.Member] = TsKnockoutModelGenerator.KoInterface;
            return obj;
        }

        public static TypeScriptFluentModuleMember AsPoco(this TypeScriptFluentModuleMember obj)
        {
            TsKnockoutModelGenerator.Tags[obj.Member] = TsKnockoutModelGenerator.Poco;
            return obj;
        }
    }
}