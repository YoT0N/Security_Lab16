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
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при зчитуванні файлу: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
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

                // Відображаємо інформацію про Крок 1
                txtStatus.Text = $"Крок 1 завершено:\n" +
                                 $"Сторона A (ID: {senderUser.Id}) зашифрувала повідомлення та обчислила хеш.\n" +
                                 $"A -> ЦП: (IdA: {senderUser.Id}, IdB: {receiverUser.Id}, C (зашифроване повідомлення), H(M) (хеш))";

                // Активуємо кнопку для Кроку 2
                btnStep2.IsEnabled = true;

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
                                     $"Хеш-образи співпадають. Документ не був модифікований.";

                    // Активуємо кнопку для Кроку 3
                    btnStep3.IsEnabled = true;
                    UpdateEncryptionStatus("Крок 2", true);
                }
                else
                {
                    txtStatus.Text = "Крок 2: Помилка! Хеш-образи не співпадають. Документ був модифікований або пошкоджений.";
                    UpdateEncryptionStatus("Крок 2", false);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка в Кроці 2: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                byte[] encryptedForReceiver = EncryptMessage(originalMessage, receiverKey);

                txtStatus.Text = $"Крок 3 завершено:\n" +
                                 $"ЦП зашифрував повідомлення ключем отримувача {receiverUser.Id}.\n" +
                                 $"ЦП -> B: (IdA: {senderUser.Id}, C' (зашифроване повідомлення для B), H(M) (хеш))";

                // Зберігаємо зашифроване повідомлення для отримувача
                encryptedForSender = encryptedForReceiver;

                // Активуємо кнопку для Кроку 4
                btnStep4.IsEnabled = true;
                UpdateEncryptionStatus("Крок 3", true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка в Кроці 3: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateEncryptionStatus("Крок 3", false);
            }
        }

        private void btnStep4_Click(object sender, RoutedEventArgs e)
        {
            // Крок 4: Сторона B розшифровує повідомлення та перевіряє хеш-образ
            try
            {
                // Сторона B розшифровує повідомлення своїм ключем
                byte[] decryptedByReceiver = DecryptMessage(encryptedForSender, receiverUser.SymmetricKey);

                // Сторона B обчислює хеш-образ розшифрованого повідомлення
                byte[] computedHashByReceiver = ComputeHash(decryptedByReceiver);

                // Сторона B порівнює обчислений хеш із отриманим від ЦП
                bool hashesMatch = CompareHashes(hashOfMessage, computedHashByReceiver);

                if (hashesMatch)
                {
                    txtStatus.Text = $"Крок 4 завершено:\n" +
                                     $"Сторона B (ID: {receiverUser.Id}) розшифрувала повідомлення своїм ключем.\n" +
                                     $"Хеш-образи співпадають. Документ підписаний стороною A (ID: {senderUser.Id}).\n" +
                                     $"ЕЦП підтверджено!";
                    UpdateEncryptionStatus("Крок 4", true);
                }
                else
                {
                    txtStatus.Text = "Крок 4: Помилка! Хеш-образи не співпадають. ЕЦП не підтверджено.";
                    UpdateEncryptionStatus("Крок 4", false);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка в Кроці 4: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateEncryptionStatus("Крок 4", false);
            }
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            // Скидаємо всі дані та інтерфейс
            originalMessage = null;
            encryptedForSender = null;
            hashOfMessage = null;
            selectedFileName = null;

            txtSelectedFile.Text = "";
            txtStatus.Text = "Готовий до роботи. Спочатку потрібно зареєструвати користувачів.";

            // Вимикаємо кнопки кроків
            btnStep2.IsEnabled = false;
            btnStep3.IsEnabled = false;
            btnStep4.IsEnabled = false;

            // Скидаємо статус шифрування
            UpdateEncryptionStatus("Крок 1", null);
            UpdateEncryptionStatus("Крок 2", null);
            UpdateEncryptionStatus("Крок 3", null);
            UpdateEncryptionStatus("Крок 4", null);
        }

        private void UpdateEncryptionStatus(string step, bool? success)
        {
            Brush statusBrush = null;
            string statusText = "";

            if (success == null)
            {
                statusText = "Очікується";
                statusBrush = Brushes.Gray;
            }
            else if (success == true)
            {
                statusText = "Успішно";
                statusBrush = Brushes.Green;
            }
            else
            {
                statusText = "Помилка";
                statusBrush = Brushes.Red;
            }

            switch (step)
            {
                case "Крок 1":
                    txtStep1Status.Text = statusText;
                    txtStep1Status.Foreground = statusBrush;
                    break;
                case "Крок 2":
                    txtStep2Status.Text = statusText;
                    txtStep2Status.Foreground = statusBrush;
                    break;
                case "Крок 3":
                    txtStep3Status.Text = statusText;
                    txtStep3Status.Foreground = statusBrush;
                    break;
                case "Крок 4":
                    txtStep4Status.Text = statusText;
                    txtStep4Status.Foreground = statusBrush;
                    break;
            }
        }

        // Криптографічні функції

        private byte[] EncryptMessage(byte[] data, byte[] key)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                // Генеруємо IV (вектор ініціалізації)
                aes.GenerateIV();
                byte[] iv = aes.IV;

                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                {
                    // Записуємо IV у початок шифрованого повідомлення
                    ms.Write(iv, 0, iv.Length);

                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(data, 0, data.Length);
                        cs.FlushFinalBlock();
                    }

                    return ms.ToArray();
                }
            }
        }

        private byte[] DecryptMessage(byte[] encryptedData, byte[] key)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                // Отримуємо IV з початку зашифрованого повідомлення
                byte[] iv = new byte[aes.BlockSize / 8];
                Array.Copy(encryptedData, 0, iv, 0, iv.Length);
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(
                        new MemoryStream(encryptedData, iv.Length, encryptedData.Length - iv.Length),
                        decryptor, CryptoStreamMode.Read))
                    {
                        byte[] buffer = new byte[1024];
                        int bytesRead;
                        while ((bytesRead = cs.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            ms.Write(buffer, 0, bytesRead);
                        }
                    }

                    return ms.ToArray();
                }
            }
        }

        private byte[] ComputeHash(byte[] data)
        {
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(data);
            }
        }

        private bool CompareHashes(byte[] hash1, byte[] hash2)
        {
            if (hash1.Length != hash2.Length)
                return false;

            for (int i = 0; i < hash1.Length; i++)
            {
                if (hash1[i] != hash2[i])
                    return false;
            }

            return true;
        }
    }

    // Клас для зберігання інформації про користувача
    public class User
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public byte[] SymmetricKey { get; set; }

        public override string ToString()
        {
            return $"{Name} (ID: {Id})";
        }
    }
}
