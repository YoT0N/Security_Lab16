using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Security.Cryptography;
using Microsoft.Win32;
using System.IO;

namespace lab16
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Центр Підпису (ЦП) зберігає відомості про користувачів
        private Dictionary<string, User> registeredUsers = new Dictionary<string, User>();

        // Поточні користувачі для демонстрації
        private User senderUser;
        private User receiverUser;

        // Інформація про підписаний документ
        private byte[] originalMessage;
        private byte[] encryptedForSender;
        private byte[] hashOfMessage;
        private string selectedFileName;

        // Для відображення в hex-форматі
        private const int BYTES_PER_LINE = 16;

        public MainWindow()
        {
            InitializeComponent();
            InitializeUI();
        }

        private void InitializeUI()
        {
            // Початкове налаштування UI
            txtStatus.Text = "Готовий до роботи. Спочатку потрібно зареєструвати користувачів.";
            UpdateRegisteredUsersList();
        }

        private void UpdateRegisteredUsersList()
        {
            lstUsers.Items.Clear();
            foreach (var user in registeredUsers.Values)
            {
                lstUsers.Items.Add($"ID: {user.Id} - {user.Name}");
            }
        }

        private void btnRegisterUser_Click(object sender, RoutedEventArgs e)
        {
            string userName = txtUserName.Text.Trim();
            if (string.IsNullOrEmpty(userName))
            {
                MessageBox.Show("Введіть ім'я користувача!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Генеруємо унікальний ідентифікатор
            string userId = Guid.NewGuid().ToString().Substring(0, 8);

            // Генеруємо ключ для симетричного шифрування (AES)
            byte[] symmetricKey = GenerateSymmetricKey();

            // Створюємо нового користувача
            User newUser = new User
            {
                Id = userId,
                Name = userName,
                SymmetricKey = symmetricKey
            };

            // Додаємо користувача до бази даних ЦП
            registeredUsers.Add(userId, newUser);

            // Оновлюємо список користувачів
            UpdateRegisteredUsersList();

            // Оновлюємо комбо-бокси вибору відправника та отримувача
            UpdateSenderReceiverComboBoxes();

            txtUserName.Clear();
            txtStatus.Text = $"Користувача '{userName}' зареєстровано з ID: {userId}";
        }

        private void UpdateSenderReceiverComboBoxes()
        {
            // Оновлюємо список відправників
            cmbSender.Items.Clear();
            cmbReceiver.Items.Clear();

            foreach (var user in registeredUsers.Values)
            {
                cmbSender.Items.Add(user);
                cmbReceiver.Items.Add(user);
            }
        }

        private byte[] GenerateSymmetricKey()
        {
            using (var aes = Aes.Create())
            {
                aes.GenerateKey();
                return aes.Key;
            }
        }

        private void btnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Текстові файли (*.txt)|*.txt|Всі файли (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Зчитуємо файл як масив байтів
                    originalMessage = File.ReadAllBytes(openFileDialog.FileName);
                    selectedFileName = System.IO.Path.GetFileName(openFileDialog.FileName);
                    txtSelectedFile.Text = selectedFileName;
                    txtStatus.Text = $"Файл '{selectedFileName}' завантажено для підпису.";

                    // Відображаємо оригінальний вміст файлу
                    DisplayOriginalMessage();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при зчитуванні файлу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DisplayOriginalMessage()
        {
            if (originalMessage == null || originalMessage.Length == 0)
            {
                txtOriginalMessage.Text = string.Empty;
                return;
            }

            // Спроба відобразити як текст, якщо можливо
            try
            {
                string textContent = Encoding.UTF8.GetString(originalMessage);

                // Перевіряємо, чи файл текстовий
                bool isText = true;
                foreach (var b in originalMessage)
                {
                    if (b < 32 && b != 9 && b != 10 && b != 13)
                    {
                        isText = false;
                        break;
                    }
                }

                if (isText)
                {
                    txtOriginalMessage.Text = textContent;
                }
                else
                {
                    // Відображаємо дані у шістнадцятковому форматі
                    txtOriginalMessage.Text = ByteArrayToHexString(originalMessage);
                }
            }
            catch
            {
                // У випадку помилки декодування, відображаємо як шістнадцяткові значення
                txtOriginalMessage.Text = ByteArrayToHexString(originalMessage);
            }
        }

        private string ByteArrayToHexString(byte[] data)
        {
            StringBuilder result = new StringBuilder();

            for (int i = 0; i < data.Length; i += BYTES_PER_LINE)
            {
                // Додаємо адресу (offset)
                result.AppendFormat("{0:X8}: ", i);

                // Додаємо шістнадцяткові значення
                for (int j = 0; j < BYTES_PER_LINE; j++)
                {
                    if (i + j < data.Length)
                        result.AppendFormat("{0:X2} ", data[i + j]);
                    else
                        result.Append("   ");
                }

                result.Append(" | ");

                // Додаємо ASCII представлення
                for (int j = 0; j < BYTES_PER_LINE; j++)
                {
                    if (i + j < data.Length)
                    {
                        char c = (char)data[i + j];
                        if (c >= 32 && c <= 126) // Відображувані символи
                            result.Append(c);
                        else
                            result.Append('.');
                    }
                }

                result.AppendLine();
            }

            return result.ToString();
        }

        private string HashToHexString(byte[] hash)
        {
            if (hash == null)
                return string.Empty;

            StringBuilder sb = new StringBuilder();
            foreach (byte b in hash)
            {
                sb.AppendFormat("{0:X2}", b);
            }
            return sb.ToString();
        }

        private void DisplayEncryptedMessage()
        {
            if (encryptedForSender == null || encryptedForSender.Length == 0)
            {
                txtEncryptedMessage.Text = string.Empty;
                return;
            }

            txtEncryptedMessage.Text = ByteArrayToHexString(encryptedForSender);
        }

        private void DisplayMessageHash()
        {
            if (hashOfMessage == null || hashOfMessage.Length == 0)
            {
                txtMessageHash.Text = string.Empty;
                return;
            }

            // Відображаємо хеш у шістнадцятковому форматі
            txtMessageHash.Text = HashToHexString(hashOfMessage);
        }

        private void btnStep1_Click(object sender, RoutedEventArgs e)
        {
            // Крок 1: Сторона A генерує повідомлення, шифрує його та відправляє ЦП

            // Перевіряємо, чи вибрано відправника
            if (cmbSender.SelectedItem == null)
            {
                MessageBox.Show("Виберіть відправника (сторона A)!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Перевіряємо, чи вибрано отримувача
            if (cmbReceiver.SelectedItem == null)
            {
                MessageBox.Show("Виберіть отримувача (сторона B)!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Перевіряємо, чи завантажено файл
            if (originalMessage == null || originalMessage.Length == 0)
            {
                MessageBox.Show("Спочатку завантажте файл для підпису!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Сторона A (відправник)
                senderUser = (User)cmbSender.SelectedItem;
                receiverUser = (User)cmbReceiver.SelectedItem;

                // Шифруємо повідомлення ключем відправника
                encryptedForSender = EncryptMessage(originalMessage, senderUser.SymmetricKey);

                // Обчислюємо хеш-образ оригінального повідомлення
                hashOfMessage = ComputeHash(originalMessage);

                // Відображаємо зашифроване повідомлення та хеш
                DisplayEncryptedMessage();
                DisplayMessageHash();

                // Відображаємо інформацію про Крок 1
                txtStatus.Text = $"Крок 1 завершено:\n" +
                                 $"Сторона A (ID: {senderUser.Id}) зашифрувала повідомлення та обчислила хеш.\n" +
                                 $"A -> ЦП: (IdA: {senderUser.Id}, IdB: {receiverUser.Id}, C (зашифроване повідомлення), H(M) (хеш))\n\n" +
                                 $"Розмір повідомлення: {originalMessage.Length} байт\n" +
                                 $"Розмір зашифрованого: {encryptedForSender.Length} байт\n" +
                                 $"Розмір хешу (SHA-256): {hashOfMessage.Length} байт";

                // Активуємо кнопку для Кроку 2
                btnStep2.IsEnabled = true;

                // Активуємо кнопки модифікації
                btnModifyEncrypted.IsEnabled = true;
                btnModifyHash.IsEnabled = true;

                // Оновлюємо інтерфейс
                UpdateEncryptionStatus("Крок 1", true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка в Кроці 1: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateEncryptionStatus("Крок 1", false);
            }
        }

        private void btnStep2_Click(object sender, RoutedEventArgs e)
        {
            // Крок 2: ЦП розшифровує повідомлення та перевіряє хеш-образ
            try
            {
                // ЦП бере з бази даних ключ відправника
                byte[] senderKey = senderUser.SymmetricKey;

                // ЦП розшифровує повідомлення
                byte[] decryptedMessage = DecryptMessage(encryptedForSender, senderKey);

                // ЦП обчислює хеш-образ розшифрованого повідомлення
                byte[] computedHash = ComputeHash(decryptedMessage);

                // ЦП порівнює обчислений хеш із отриманим
                bool hashesMatch = CompareHashes(hashOfMessage, computedHash);

                if (hashesMatch)
                {
                    txtStatus.Text = $"Крок 2 завершено:\n" +
                                     $"ЦП розшифрував повідомлення ключем відправника {senderUser.Id}.\n" +
                                     $"Обчислений хеш: {HashToHexString(computedHash)}\n" +
                                     $"Отриманий хеш: {HashToHexString(hashOfMessage)}\n" +
                                     $"Хеш-образи співпадають. Документ не був модифікований.";

                    // Активуємо кнопку для Кроку 3
                    btnStep3.IsEnabled = true;
                    UpdateEncryptionStatus("Крок 2", true);
                }
                else
                {
                    txtStatus.Text = $"Крок 2: Помилка! Хеш-образи не співпадають.\n" +
                                    $"Обчислений хеш: {HashToHexString(computedHash)}\n" +
                                    $"Отриманий хеш: {HashToHexString(hashOfMessage)}\n" +
                                    $"Документ був модифікований або пошкоджений.";
                    UpdateEncryptionStatus("Крок 2", false);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка в Кроці 2: {ex.Message}\n\nЦе може вказувати на підміну зашифрованого повідомлення.",
                                "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateEncryptionStatus("Крок 2", false);
            }
        }

        private void btnStep3_Click(object sender, RoutedEventArgs e)
        {
            // Крок 3: ЦП зашифровує повідомлення ключем отримувача та надсилає стороні B
            try
            {
                // ЦП бере з бази даних ключ отримувача
                byte[] receiverKey = receiverUser.SymmetricKey;

                // ЦП зашифровує оригінальне повідомлення ключем отримувача
                byte[] encryptedFor