using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

const string queueName = "polyclinic";
var patients = CreatePatientsList(GetNumberOfPatients());
var connection = CreateRabbitMqConnection();
SendMessageToQueue(patients, connection);
ListenToQueue(connection);

static int GetNumberOfPatients()
{
    Console.WriteLine("Введите количество пациентов:");
    int numberOfPatients;
    while (true)
    {
        if (int.TryParse(Console.ReadLine(), out numberOfPatients) && numberOfPatients > 0)
        {
            return numberOfPatients;
        }
        Console.WriteLine("Введите корректное число:");
    }
}

static List<Patient> CreatePatientsList(int numPatients)
{
    var patients = new List<Patient>();
    for (int i = 0; i < numPatients; i++)
    {
        patients.Add(CreateRandomPatient());
    }
    return patients;
}

static Patient CreateRandomPatient()
{
    var random = new Random();
    var patientTypes = new[]
    {
        new { Type = PatientType.JustAsk, TimeInQueue = 1, InQueueAgain = false },
        new { Type = PatientType.Normal, TimeInQueue = 5, InQueueAgain = false },
        new { Type = PatientType.Grandma, TimeInQueue = 10, InQueueAgain = true },
    };
    var index = random.Next(patientTypes.Length);
    return new Patient
    {
        Type = patientTypes[index].Type,
        TimeInQueue = patientTypes[index].TimeInQueue,
        InQueueAgain = patientTypes[index].InQueueAgain
    };
}

static IConnection CreateRabbitMqConnection()
{
    var factory = new ConnectionFactory
    {
        HostName = "192.168.101.17",
        UserName = "test",
        Password = "test",
        Port = 5672
    };

    try
    {
        var connection = factory.CreateConnection();
        var channel = connection.CreateModel();
        channel.QueueDeclare(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);
        return connection;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{DateTime.Now} Ошибка при создании соединения с RabbitMQ: {ex.Message}");
        throw;
    }
}

static void SendMessageToQueue(List<Patient> patients, IConnection connection)
{
    using var channel = connection.CreateModel();
    try
    {
        foreach (var patient in patients)
        {
           var body = Encoding.Default.GetBytes(JsonConvert.SerializeObject(patient));
           channel.BasicPublish(exchange: "",
                             routingKey: queueName,
                             basicProperties: null,
                             body: body);
         }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{DateTime.Now} Ошибка при отправке сообщения: {ex.Message}");
    }
}

static void ListenToQueue(IConnection connection)
{
    if (connection == null || !connection.IsOpen)
    {
        Console.WriteLine($"{DateTime.Now} Ошибка при подключении к RabbitMQ: соединение не установлено.");
        return;
    }
    using var channel = connection.CreateModel();
    try
    {
        var consumer = new EventingBasicConsumer(channel);

        consumer.Received += (sender, e) =>
        {
            var bodyString = Encoding.UTF8.GetString(e.Body.ToArray());
            if (bodyString != null)
            {
                var patient = JsonConvert.DeserializeObject<Patient>(bodyString);
                if (patient != null)
                    GetMessage(patient, channel);
            }
            else
            {
                throw new Exception("NullReferenceException");
            }
        };

        channel.BasicConsume(queue: queueName,
                             autoAck: true,
                             consumer: consumer);

        Thread.Sleep(Timeout.Infinite);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{DateTime.Now} Ошибка при чтении очереди: {ex.Message}");
    }
}


static void GetMessage(Patient patient, IModel channel)
{
    try
    {
        Console.WriteLine($"{DateTime.Now} {patient.Type} Вход в кабинет");
        Thread.Sleep(patient.TimeInQueue * 1000);
        Console.WriteLine($"{DateTime.Now} {patient.Type} Выход из кабинета");
        if (patient.InQueueAgain)
        {
            patient.InQueueAgain = false;
            var body = Encoding.Default.GetBytes(JsonConvert.SerializeObject(patient));
            try
            {
                channel.BasicPublish(exchange: "",
                                 routingKey: queueName,
                                 basicProperties: null,
                                 body: body);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now} Ошибка при повторной отправке сообщения: {ex.Message}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{DateTime.Now} Ошибка при выводе сообщения: {ex.Message}");
    }
}


public enum PatientType
{
    JustAsk,
    Normal,
    Grandma
}

public class Patient
{
    public PatientType Type { get; set; }
    public int TimeInQueue { get; set; }
    public bool InQueueAgain { get; set; }
}