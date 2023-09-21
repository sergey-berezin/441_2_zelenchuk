using AIPack;

AIManager aIManager = new AIManager();

aIManager.CallModel("bird.jpg");

File.Create("./test.txt");

//public interface ISave {
//    public string Name { get; set; }
//    public void Save();
//}