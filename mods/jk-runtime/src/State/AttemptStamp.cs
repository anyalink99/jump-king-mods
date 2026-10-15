namespace JKRuntime.State
{
    public struct AttemptStamp
    {
        public int Session;
        public int Attempt;
        public bool Equals(AttemptStamp other) { return Session == other.Session && Attempt == other.Attempt; }
    }
}
