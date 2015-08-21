namespace ExternalHandlerContainerExample
{
    public class PingMessage
    {
        public string Text { get; set; }
        public int Generation { get; set; }
    }

    public class PongMessage
    {
        public string ReplyText { get; set; }
    }
}
