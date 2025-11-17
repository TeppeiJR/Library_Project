using Library_Project.Model;
using Library_Project.Services;
using Library_Project.Services.Interfaces;
using Moq;

namespace Library_Project_Tests
{
    public class LibraryServiceTests
    {
        private readonly Mock<IBookRepository> _repoMock;
        private readonly Mock<IMemberService> _memberMock;
        private readonly Mock<INotificationService> _notifMock;
        private readonly LibraryService _service;

        public LibraryServiceTests()
        {
            _repoMock = new Mock<IBookRepository>();
            _memberMock = new Mock<IMemberService>();
            _notifMock = new Mock<INotificationService>();

            _service = new LibraryService(_repoMock.Object, _memberMock.Object, _notifMock.Object);
        }

        /// <summary>
        /// ([Theory], [InlineData], Assert.Throws)
        /// Перевіряє, що сервіс кидає ArgumentException при спробі додати книгу з невалідними даними
        /// </summary>
        [Theory]
        [InlineData(null, 5)]   
        [InlineData("Valid Title", 0)]
        [InlineData("   ", 3)] 
        [InlineData("Another Title", -1)] 
        public void AddBook_ShouldThrowArgumentException_WhenInputIsInvalid(string title, int copies)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _service.AddBook(title, copies));
        }

        /// <summary>
        /// (Verify(..., Times.Exactly(n)), It.Is(predicate))
        /// Перевіряє, що при додаванні нової книги, яка ще не існує в репозиторії, метод SaveBook викликається 1 раз з коректно створеним об'єктом Book.
        /// </summary>
        [Fact]
        public void AddBook_ShouldCreateNewBook_WhenBookDoesNotExist()
        {
            // Arrange
            const string NEWTITLE = "The little prince";
            int initialCopies = 5;
            _repoMock.Setup(r => r.FindBook(NEWTITLE)).Returns((Book)null);

            // Act
            _service.AddBook(NEWTITLE, initialCopies);

            // Assert
            _repoMock.Verify(r => r.SaveBook(It.Is<Book>(b => b.Title == NEWTITLE && b.Copies == initialCopies)), Times.Exactly(1));
        }

        /// <summary>
        /// (Assert.Equal, Verify())
        /// Перевіряє, що при додаванні копій до існуючої книги, кількість копій коректно сумується, і книга зберігається.
        /// </summary>
        [Fact]
        public void AddBook_ShouldUpdateCopies_WhenBookAlreadyExists()
        {
            // Arrange
            const string EXISTINGTITLE = "Atomic Habits";
            var existingBook = new Book { Title = EXISTINGTITLE, Copies = 7 };
            _repoMock.Setup(r => r.FindBook(EXISTINGTITLE)).Returns(existingBook);

            // Act
            _service.AddBook(EXISTINGTITLE, 3);

            // Assert
            Assert.Equal(10, existingBook.Copies);
            _repoMock.Verify(r => r.SaveBook(existingBook));
        }

        /// <summary>
        /// (Assert.True, Verify(..., Times.Exactly(n)))
        /// Перевіряє успішну видачу книги: метод має повернути true, кількість копій зменшитись, а сповіщення - надіслатись.
        /// </summary>
        [Fact]
        public void BorrowBook_ShouldSucceed_WhenBookIsAvailableAndMemberIsValid()
        {
            const string TITLE = "Aeneid";
            // Arrange
            var book = new Book { Title = TITLE, Copies = 5 };
            _repoMock.Setup(r => r.FindBook(TITLE)).Returns(book);
            _memberMock.Setup(m => m.IsValidMember(1)).Returns(true);

            // Act
            bool result = _service.BorrowBook(1, TITLE);

            // Assert
            Assert.True(result);
            Assert.Equal(4, book.Copies);
            _repoMock.Verify(r => r.SaveBook(book), Times.Exactly(1));
            _notifMock.Verify(n => n.NotifyBorrow(1, TITLE), Times.Exactly(1));
        }

        /// <summary>
        /// (Assert.False, Verify(..., Times.Never, It.IsAny())
        /// Перевіряє випадок, коли книги немає в наявності (0 примірників).
        /// </summary>
        [Fact]
        public void BorrowBook_BookNotAvailable_ReturnsFalse()
        {
            const string TITLE = "Aeneid";
            var book = new Book { Title = TITLE, Copies = 0 };
            _repoMock.Setup(r => r.FindBook(TITLE)).Returns(book);
            _memberMock.Setup(m => m.IsValidMember(1)).Returns(true);

            bool result = _service.BorrowBook(1, TITLE);

            Assert.False(result);
            _notifMock.Verify(n => n.NotifyBorrow(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// (Assert.False)
        /// Перевіряє випадок, коли книга не знайдена в репозиторії.
        /// </summary>
        [Fact]
        public void BorrowBook_BookNotFound_ReturnsFalse()
        {
            // Arrange
            int memberId = 1;
            const string TITLE = "Aeneid";

            _memberMock.Setup(s => s.IsValidMember(memberId)).Returns(true);
            _repoMock.Setup(r => r.FindBook(TITLE)).Returns((Book)null);

            // Act
            var result = _service.BorrowBook(memberId, TITLE);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// (Assert.Throws, Verify(..., Times.Never), It.IsAny())
        /// Перевіряє, що невалідний користувач не може взяти книгу.
        /// </summary>
        [Fact]
        public void BorrowBook_InvalidMember_ThrowsInvalidOperationException()
        {
            // Arrange
            int invalidMemberId = 99;
            _memberMock.Setup(m => m.IsValidMember(invalidMemberId)).Returns(false);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _service.BorrowBook(invalidMemberId, "Aeneid"));
            _repoMock.Verify(r => r.SaveBook(It.IsAny<Book>()), Times.Never);
            _notifMock.Verify(n => n.NotifyBorrow(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// (Assert.True, Verify(..., Times.Exactly(n)), Assert.Equal)
        /// Перевіряє успішне повернення книги.
        /// </summary>
        [Fact]
        public void ReturnBook_ExistingBook_ReturnsTrueAndUpdatesCopies()
        {
            // Arrange
            int memberId = 1;
            const string TITLE = "Atomic Habits";
            var book = new Book { Title = TITLE, Copies = 3 };
            _repoMock.Setup(r => r.FindBook(TITLE)).Returns(book);

            // Act
            bool result = _service.ReturnBook(memberId, TITLE);

            // Assert
            Assert.True(result);
            Assert.Equal(4, book.Copies);
            _repoMock.Verify(r => r.SaveBook(book), Times.Exactly(1));
            _notifMock.Verify(n => n.NotifyReturn(memberId, TITLE), Times.Exactly(1));
        }

        /// <summary>
        /// (Assert.False)
        /// Перевіряє негативний сценарій, коли користувач намагається повернути книгу, якої не має в каталозі.
        /// </summary>
        [Fact]
        public void ReturnBook_BookNotFound_ReturnFalse()
        {
            // Arrange
            const string TITLE = "Atomic Habits";
            _repoMock.Setup(r => r.FindBook(TITLE)).Returns((Book)null);

            // Act
            bool result = _service.ReturnBook(1, TITLE);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// (Assert.NotNull, NotEmpty, Contains, Equal)
        /// Перевіряє сценарій, коли є суміш доступних і недоступних книг.
        /// </summary>
        [Fact]
        public void GetAvailableBooks_WithMixedAvailability_ShouldReturnOnlyAvailable()
        {
            // Arrange
            var all = new List<Book>
            {
                new Book { Title = "The little prince", Copies = 0 },
                new Book { Title = "Aeneid", Copies = 1 },
                new Book { Title = "Atomic Habits", Copies = 3 }
            };
            _repoMock.Setup(r => r.GetAllBooks()).Returns(all);

            // Act
            var result = _service.GetAvailableBooks();

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.Contains(result, b => b.Title == "Aeneid");
            Assert.Contains(result, b => b.Title == "Atomic Habits");
            Assert.Equal(2, result.Count);
        }

        /// <summary>
        /// (Assert.Empty)
        /// Повинен повертати порожній список, коли немає доступних.
        /// </summary>
        [Fact]
        public void GetAvailableBooks_WhenNoneAvailable_ShouldReturnEmptyList()
        {

            var all = new List<Book>
            {
                new Book { Title = "Aeneid", Copies = 0 },
                new Book { Title = "Atomic Habits", Copies = 0 }
            };
            _repoMock.Setup(r => r.GetAllBooks()).Returns(all);

            // Act
            var result = _service.GetAvailableBooks();

            // Assert
            Assert.Empty(result);
        }
    }
}
