using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MyFirstCRM
{
    public partial class UserManagementWindow : Window
    {
        private List<User> _allUsers = new();
        private User? _editingUser;

        public UserManagementWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Проверка прав доступа
            if (!MultiUserSecurityManager.IsAdmin)
            {
                MessageBox.Show("Управление пользователями доступно только администраторам",
                    "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }

            // Загрузка информации о компании
            if (MultiUserSecurityManager.CurrentCompany != null)
            {
                CompanyInfoText.Text = $"{MultiUserSecurityManager.CurrentCompany.Name} " +
                    $"(Код: {MultiUserSecurityManager.CurrentCompany.CompanyCode})";
                UsersCountText.Text = $"0 / {MultiUserSecurityManager.CurrentCompany.MaxUsers}";
            }

            LoadUsers();
        }

        private void LoadUsers()
        {
            if (MultiUserSecurityManager.CurrentCompany == null) return;

            _allUsers = MultiUserSecurityManager.GetCompanyUsers(MultiUserSecurityManager.CurrentCompany.Id);
            ApplyFilters();

            // Обновление счетчика
            var company = MultiUserSecurityManager.CurrentCompany;
            UsersCountText.Text = $"{_allUsers.Count} / {company.MaxUsers}";
        }

        private void ApplyFilters()
        {
            var filtered = _allUsers.AsEnumerable();

            // Фильтр по поиску
            var searchText = SearchTextBox.Text?.ToLower() ?? "";
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filtered = filtered.Where(u =>
                    u.FullName.ToLower().Contains(searchText) ||
                    u.Username.ToLower().Contains(searchText) ||
                    (u.Email?.ToLower().Contains(searchText) ?? false));
            }

            // Фильтр по роли
            if (RoleFilterComboBox.SelectedIndex > 0)
            {
                var role = RoleFilterComboBox.SelectedIndex - 1;
                filtered = filtered.Where(u => (int)u.Role == role);
            }

            RenderUsersList(filtered.ToList());
        }

        private void RenderUsersList(List<User> users)
        {
            if (UsersListPanel == null) return;
            
            UsersListPanel.Children.Clear();

            if (users == null || users.Count == 0)
            {
                UsersListPanel.Children.Add(new TextBlock
                {
                    Text = "Нет пользователей для отображения",
                    Foreground = new SolidColorBrush(Colors.Gray),
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 50, 0, 0)
                });
                return;
            }

            foreach (var user in users)
            {
                var userCard = CreateUserCard(user);
                UsersListPanel.Children.Add(userCard);
            }
        }

        private Border CreateUserCard(User user)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 10),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });

            // Аватар
            var avatarBorder = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(20),
                Background = GetRoleColor(user.Role),
                Child = new TextBlock
                {
                    Text = !string.IsNullOrEmpty(user.FullName) ? user.FullName[..1].ToUpper() : "?",
                    Foreground = new SolidColorBrush(Colors.White),
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(avatarBorder, 0);
            grid.Children.Add(avatarBorder);

            // Информация
            var infoPanel = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
            infoPanel.Children.Add(new TextBlock
            {
                Text = user.FullName,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59))
            });
            infoPanel.Children.Add(new TextBlock
            {
                Text = $"@{user.Username} • {user.Email ?? "Нет email"}",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 2, 0, 0)
            });
            if (!string.IsNullOrEmpty(user.Position))
            {
                infoPanel.Children.Add(new TextBlock
                {
                    Text = $"{user.Position}{(string.IsNullOrEmpty(user.Department) ? "" : $", {user.Department}")}",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }
            Grid.SetColumn(infoPanel, 1);
            grid.Children.Add(infoPanel);

            // Роль
            var roleBorder = new Border
            {
                Background = GetRoleLightColor(user.Role),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock
                {
                    Text = GetRoleName(user.Role),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = GetRoleColor(user.Role)
                }
            };
            Grid.SetColumn(roleBorder, 2);
            grid.Children.Add(roleBorder);

            // Статус
            var statusText = new TextBlock
            {
                Text = user.IsActive ? "✓ Активен" : "✗ Отключен",
                FontSize = 12,
                Foreground = user.IsActive
                    ? new SolidColorBrush(Color.FromRgb(22, 163, 74))
                    : new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(statusText, 3);
            grid.Children.Add(statusText);

            // Кнопки действий
            var actionsPanel = new StackPanel { Orientation = Orientation.Horizontal };

            if (!user.IsPrimaryAdmin) // Нельзя редактировать главного админа
            {
                var editBtn = new Button
                {
                    Content = "✎",
                    Background = new SolidColorBrush(Colors.Transparent),
                    Foreground = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(8),
                    FontSize = 14,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = user
                };
                editBtn.Click += OnEditUserClick;
                actionsPanel.Children.Add(editBtn);

                if (user.Id != MultiUserSecurityManager.CurrentUser?.Id) // Нельзя удалить себя
                {
                    var deleteBtn = new Button
                    {
                        Content = "🗑",
                        Background = new SolidColorBrush(Colors.Transparent),
                        Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(8),
                        FontSize = 14,
                        Cursor = System.Windows.Input.Cursors.Hand,
                        Tag = user
                    };
                    deleteBtn.Click += OnDeleteUserClick;
                    actionsPanel.Children.Add(deleteBtn);
                }
            }

            Grid.SetColumn(actionsPanel, 4);
            grid.Children.Add(actionsPanel);

            border.Child = grid;
            return border;
        }

        private SolidColorBrush GetRoleColor(UserRole role)
        {
            return role switch
            {
                UserRole.Admin => new SolidColorBrush(Color.FromRgb(147, 51, 234)),    // Фиолетовый
                UserRole.Manager => new SolidColorBrush(Color.FromRgb(59, 130, 246)),    // Синий
                UserRole.Employee => new SolidColorBrush(Color.FromRgb(16, 185, 129)),   // Зеленый
                _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))                  // Серый
            };
        }

        private SolidColorBrush GetRoleLightColor(UserRole role)
        {
            return role switch
            {
                UserRole.Admin => new SolidColorBrush(Color.FromRgb(243, 232, 255)),
                UserRole.Manager => new SolidColorBrush(Color.FromRgb(219, 234, 254)),
                UserRole.Employee => new SolidColorBrush(Color.FromRgb(220, 252, 231)),
                _ => new SolidColorBrush(Color.FromRgb(241, 245, 249))
            };
        }

        private string GetRoleName(UserRole role)
        {
            return role switch
            {
                UserRole.Admin => "Администратор",
                UserRole.Manager => "Менеджер",
                UserRole.Employee => "Сотрудник",
                _ => "Только просмотр"
            };
        }

        // === ОБРАБОТЧИКИ СОБЫТИЙ ===

        private void OnSearchChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void OnRoleFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void OnAddUserClick(object sender, RoutedEventArgs e)
        {
            _editingUser = null;
            EditPanelTitle.Text = "Новый пользователь";
            EditPasswordBox.Password = "";
            EditPasswordBox.IsEnabled = true;
            EditErrorBorder.Visibility = Visibility.Collapsed;

            // Очистка полей
            EditUsernameTextBox.Text = "";
            EditFullNameTextBox.Text = "";
            EditEmailTextBox.Text = "";
            EditPhoneTextBox.Text = "";
            EditPositionTextBox.Text = "";
            EditDepartmentTextBox.Text = "";
            EditRoleComboBox.SelectedIndex = 2; // Employee

            UserEditPanel.Visibility = Visibility.Visible;
            UsersListPanel.IsEnabled = false;
        }

        private void OnEditUserClick(object sender, RoutedEventArgs e)
        {
            var user = (sender as Button)?.Tag as User;
            if (user == null) return;

            _editingUser = user;
            EditPanelTitle.Text = "Редактирование пользователя";
            EditPasswordBox.Password = "";
            EditPasswordBox.IsEnabled = false; // При редактировании пароль не меняется по умолчанию
            EditErrorBorder.Visibility = Visibility.Collapsed;

            // Заполнение полей
            EditUsernameTextBox.Text = user.Username;
            EditFullNameTextBox.Text = user.FullName;
            EditEmailTextBox.Text = user.Email ?? "";
            EditPhoneTextBox.Text = user.Phone ?? "";
            EditPositionTextBox.Text = user.Position ?? "";
            EditDepartmentTextBox.Text = user.Department ?? "";
            EditRoleComboBox.SelectedIndex = (int)user.Role;

            UserEditPanel.Visibility = Visibility.Visible;
            UsersListPanel.IsEnabled = false;
        }

        private void OnDeleteUserClick(object sender, RoutedEventArgs e)
        {
            var user = (sender as Button)?.Tag as User;
            if (user == null) return;

            var result = MessageBox.Show(
                $"Вы уверены, что хотите удалить пользователя '{user.FullName}'?\n\n" +
                "Это действие нельзя отменить.",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var deleteResult = MultiUserSecurityManager.DeleteUser(user.Id);
                if (deleteResult.Success)
                {
                    LoadUsers();
                    MessageBox.Show(deleteResult.Message, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(deleteResult.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OnSaveUserClick(object sender, RoutedEventArgs e)
        {
            if (MultiUserSecurityManager.CurrentCompany == null) return;

            var username = EditUsernameTextBox.Text.Trim();
            var fullName = EditFullNameTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(fullName))
            {
                ShowEditError("Заполните обязательные поля (логин и ФИО)");
                return;
            }

            if (_editingUser == null) // Создание нового
            {
                var password = EditPasswordBox.Password;
                if (string.IsNullOrEmpty(password))
                {
                    ShowEditError("Для нового пользователя необходимо указать пароль");
                    return;
                }

                var role = (UserRole)EditRoleComboBox.SelectedIndex;

                var result = MultiUserSecurityManager.CreateUser(
                    companyId: MultiUserSecurityManager.CurrentCompany.Id,
                    username: username,
                    fullName: fullName,
                    password: password,
                    role: role,
                    email: EditEmailTextBox.Text.Trim(),
                    phone: EditPhoneTextBox.Text.Trim(),
                    position: EditPositionTextBox.Text.Trim(),
                    department: EditDepartmentTextBox.Text.Trim()
                );

                if (result.Success)
                {
                    LoadUsers();
                    OnCancelEditClick(null, null);
                    MessageBox.Show(result.Message, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    ShowEditError(result.Message);
                }
            }
            else // Редактирование
            {
                _editingUser.FullName = fullName;
                _editingUser.Email = EditEmailTextBox.Text.Trim();
                _editingUser.Phone = EditPhoneTextBox.Text.Trim();
                _editingUser.Position = EditPositionTextBox.Text.Trim();
                _editingUser.Department = EditDepartmentTextBox.Text.Trim();
                _editingUser.Role = (UserRole)EditRoleComboBox.SelectedIndex;

                var result = MultiUserSecurityManager.UpdateUser(_editingUser);

                if (result.Success)
                {
                    // Обновление пароля если указан
                    if (!string.IsNullOrEmpty(EditPasswordBox.Password))
                    {
                        MultiUserSecurityManager.ChangePassword(_editingUser.Id, EditPasswordBox.Password);
                    }

                    LoadUsers();
                    OnCancelEditClick(null, null);
                    MessageBox.Show(result.Message, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    ShowEditError(result.Message);
                }
            }
        }

        private void ShowEditError(string message)
        {
            EditErrorMessage.Text = message;
            EditErrorBorder.Visibility = Visibility.Visible;
        }

        private void OnCancelEditClick(object? sender, RoutedEventArgs? e)
        {
            UserEditPanel.Visibility = Visibility.Collapsed;
            UsersListPanel.IsEnabled = true;
            _editingUser = null;
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
