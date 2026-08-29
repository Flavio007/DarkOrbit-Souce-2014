using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class OreRefinementEntryCommand
    {
        public const short ID = 31038;
        private const short BEFORE_TYPE_MARKER = -7748;
        private const short AFTER_TYPE_MARKER = -8972;

        public RefinementTypeModule Source { get; }
        public OreStackCommand Result { get; }

        public OreRefinementEntryCommand(RefinementTypeModule source, OreStackCommand result)
        {
            Source = source ?? new RefinementTypeModule(RefinementTypeModule.LASER);
            Result = result ?? new OreStackCommand(0, new OreResourceTypeModule(OreResourceTypeModule.PROMETIUM));
        }

        public byte[] write()
        {
            var param1 = new ByteArray(ID);
            param1.write(Result.write());
            param1.writeShort(AFTER_TYPE_MARKER);
            param1.writeShort(BEFORE_TYPE_MARKER);
            param1.write(Source.write());
            return param1.Message.ToArray();
        }
    }
}
