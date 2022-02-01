namespace AssetStudio
{
    public class TypeTreeNode
    {
        public string type { get; set; }
        public string name { get; set; }
        //public int m_ByteSize;
        //public int m_Index;
        //public int m_IsArray; //m_TypeFlags
        //public int m_Version;
        public int meta_flag { get; set; }
        public int level { get; set; }
        //public uint m_TypeStrOffset;
        //public uint m_NameStrOffset;
        //public ulong m_RefTypeHash;

        public TypeTreeNode() { }

        public TypeTreeNode(string type, string name, int level, bool align)
        {
            this.type = type;
            this.name = name;
            this.level = level;
            this.meta_flag = align ? 0x4000 : 0;
        }
    }
}
