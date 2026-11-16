
import { useEffect, useMemo, useState } from 'react'
import { createOrdersHub, type OrderStatusNotification } from './services/ordersHub'
import './App.css'
import './AppPanels.css'

type Product = {
  id: string
  name: string
  description: string
  price: number
  currency: string
  category: string
  sellerId: string
  createdAt: string
  updatedAt: string | null
  isActive: boolean
  imageUrl?: string | null
}

type User = {
  id: string
  email: string
  firstName: string
  lastName: string
  roles: string[]
}

type CartItem = {
  product: Product
  quantity: number
}

type Order = {
  id: string
  status?: string | number
  createdAt?: string
  totalAmount?: number
  total?: number
  currency?: string
}

type PaymentInfo = {
  id: string
  orderId: string
  amount: number
  currency: string
  status: string | number
  createdAt: string
  completedAt?: string | null
  failureReason?: string | null
}

type AuthMode = 'login' | 'register'
type Panel = 'cart' | 'orders' | 'profile' | 'seller' | null

const TOKEN_KEY = 'marketflow_access_token'

const categories = [
  { name: 'Все товары', icon: '✦' },
  { name: 'Смартфоны', icon: '📱' },
  { name: 'Ноутбуки', icon: '💻' },
  { name: 'Планшеты', icon: '📲' },
  { name: 'Умные часы', icon: '⌚' },
  { name: 'Аудио', icon: '🎧' },
  { name: 'Игровые приставки', icon: '🎮' },
  { name: 'Фото и видео', icon: '📷' },
  { name: 'Аксессуары', icon: '🖱️' },
]

const formatPrice = (price: number, currency: string) =>
  new Intl.NumberFormat('ru-RU').format(price) +
  (currency === 'RUB' ? ' ₽' : ` ${currency}`)

const getProductImage = (product: Product): string | null => {
  // Отдельная фотография для PlayStation 5
  if (product.name === 'PlayStation 5 Slim Disc Edition') {
    return 'https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcTA4v658LPeIdpOPZPPrWyah-JwQrj4oMzL7JEKt1KCcQ&s=10'
  }

  if (product.imageUrl) return product.imageUrl

  if (product.name === 'Samsung Galaxy S25 Ultra') {
    return 'https://ibrat.ru/upload/cssinliner_webp/iblock/288/zanpzakde6mvmgozw3eduq4gvi03sv83.webp'
  }

  return null
}

const getProductEmoji = (category: string) => {
  switch (category.toLowerCase()) {
    case 'смартфоны':
      return '📱'
    case 'ноутбуки':
      return '💻'
    case 'планшеты':
      return '📲'
    case 'аксессуары':
      return '🖱️'
    case 'умные часы':
      return '⌚'
    case 'аудио':
      return '🎧'
    case 'игровые приставки':
      return '🎮'
    case 'фото и видео':
      return '📷'
    default:
      return '📦'
  }
}

const getProductColor = (category: string) => {
  switch (category.toLowerCase()) {
    case 'смартфоны':
      return 'lavender'
    case 'ноутбуки':
      return 'blue'
    case 'планшеты':
      return 'peach'
    case 'аксессуары':
      return 'mint'
    case 'умные часы':
      return 'peach'
    case 'аудио':
      return 'mint'
    case 'игровые приставки':
      return 'blue'
    case 'фото и видео':
      return 'lavender'
    default:
      return 'blue'
  }
}

async function readError(response: Response): Promise<string> {
  const text = await response.text()

  if (!text) {
    return `Ошибка сервера: ${response.status}`
  }

  try {
    const data = JSON.parse(text)
    return (
      data.message ||
      data.title ||
      data.detail ||
      `Ошибка сервера: ${response.status}`
    )
  } catch {
    return text
  }
}

function App() {
  const [products, setProducts] = useState<Product[]>([])
  const [stockByProduct, setStockByProduct] = useState<Record<string, number>>({})
  const [selectedProduct, setSelectedProduct] = useState<Product | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [search, setSearch] = useState('')
  const [activeCategory, setActiveCategory] = useState('Все товары')
  const [favorites, setFavorites] = useState<string[]>([])
  const [payments, setPayments] = useState<Record<string, PaymentInfo>>({})

  const [token, setToken] = useState(
    () => sessionStorage.getItem(TOKEN_KEY) || '',
  )
  const [user, setUser] = useState<User | null>(null)
  const [authChecking, setAuthChecking] = useState(true)

  const [authMode, setAuthMode] = useState<AuthMode>('login')
  const [authOpen, setAuthOpen] = useState(false)
  const [authRole, setAuthRole] = useState<'Buyer' | 'Seller'>('Buyer')
  const [authForm, setAuthForm] = useState({
    firstName: '',
    lastName: '',
    email: '',
    password: '',
  })
  const [authBusy, setAuthBusy] = useState(false)
  const [authError, setAuthError] = useState('')

  const [cart, setCart] = useState<Record<string, number>>({})
  const [panel, setPanel] = useState<Panel>(null)
  const [productFormOpen, setProductFormOpen] = useState(false)
  const [editingProduct, setEditingProduct] = useState<Product | null>(null)
  const [productBusy, setProductBusy] = useState(false)

  const [productForm, setProductForm] = useState({
    name: '',
    description: '',
    price: '',
    currency: 'RUB',
    category: 'Смартфоны',
    imageUrl: '',
    stockQuantity: '',
  })
  const [orders, setOrders] = useState<Order[]>([])
  const [ordersLoading, setOrdersLoading] = useState(false)
  const [checkoutBusy, setCheckoutBusy] = useState(false)
  const [panelError, setPanelError] = useState('')
  const [notice, setNotice] = useState('')

  useEffect(() => {
    const loadProducts = async () => {
      try {
        setLoading(true)
        setError('')

        const response = await fetch('/api/Products')

        if (!response.ok) {
          throw new Error(await readError(response))
        }

        const data: Product[] = await response.json()
        const activeProducts = data.filter((product) => product.isActive)

        setProducts(activeProducts)

        const stockEntries = await Promise.all(
          activeProducts.map(async (product) => {
            try {
              const stockResponse = await fetch(
                `/api/inventory/${product.id}`,
              )

              if (!stockResponse.ok) {
                return [product.id, 0] as const
              }

              const stock: { inStock: number } = await stockResponse.json()

              return [product.id, stock.inStock] as const
            } catch {
              return [product.id, 0] as const
            }
          }),
        )

        setStockByProduct(Object.fromEntries(stockEntries))
      } catch (err) {
        setError(
          err instanceof Error
            ? err.message
            : 'Не удалось загрузить товары',
        )
      } finally {
        setLoading(false)
      }
    }

    loadProducts()
  }, [])

  useEffect(() => {
    const restoreSession = async () => {
      if (!token) {
        setAuthChecking(false)
        return
      }

      try {
        const response = await fetch('/api/Auth/me', {
          headers: {
            Authorization: `Bearer ${token}`,
          },
        })

        if (!response.ok) {
          throw new Error('Сессия завершена')
        }

        const data: User = await response.json()
        setUser(data)
      } catch {
        sessionStorage.removeItem(TOKEN_KEY)
        setToken('')
        setUser(null)
      } finally {
        setAuthChecking(false)
      }
    }

    restoreSession()
  }, [token])

  useEffect(() => {
    if (!token || !user?.roles?.includes('Buyer')) {
      return
    }

    let isActive = true

    const connection = createOrdersHub(
      token,
      (notification: OrderStatusNotification) => {
        console.log('SIGNALR EVENT RECEIVED:', notification)
        setOrders((currentOrders) =>
          currentOrders.map((order) =>
            order.id === notification.orderId
              ? { ...order, status: notification.status }
              : order,
          ),
        )

        const statusText = getOrderStatus(notification.status)
        setNotice(
          notification.message ||
            `Статус заказа #${notification.orderId.slice(0, 8)} изменён: ${statusText}`,
        )
      },
    )

    connection
      .start()
      .catch((error) => {
        if (isActive) {
          console.error('Не удалось подключиться к уведомлениям заказов:', error)
        }
      })

    return () => {
      isActive = false
      void connection.stop()
    }
  }, [token, user])

  const filteredProducts = useMemo(() => {
    return products.filter((product) => {
      const matchesCategory =
        activeCategory === 'Все товары' ||
        product.category === activeCategory

      const normalizedSearch = search.toLowerCase().trim()

      const matchesSearch =
        product.name.toLowerCase().includes(normalizedSearch) ||
        product.description.toLowerCase().includes(normalizedSearch)

      return matchesCategory && matchesSearch
    })
  }, [products, search, activeCategory])

  const cartItems = useMemo<CartItem[]>(() => {
    return Object.entries(cart)
      .map(([id, quantity]) => {
        const product = products.find((item) => item.id === id)
        return product ? { product, quantity } : null
      })
      .filter((item): item is CartItem => item !== null)
  }, [cart, products])

  const cartCount = cartItems.reduce(
    (sum, item) => sum + item.quantity,
    0,
  )

  const cartTotal = cartItems.reduce(
    (sum, item) => sum + item.product.price * item.quantity,
    0,
  )

  const toggleFavorite = (id: string) => {
    setFavorites((current) =>
      current.includes(id)
        ? current.filter((item) => item !== id)
        : [...current, id],
    )
  }

  const addToCart = (product: Product) => {
  const available = stockByProduct[product.id] ?? 0
  const inCart = cart[product.id] ?? 0

  if (available <= 0 || inCart >= available) {
    setNotice('Недостаточно товара на складе')
    return
  }

  setCart((current) => ({
    ...current,
    [product.id]: (current[product.id] || 0) + 1,
  }))

  setNotice(`${product.name} добавлен в корзину`)
}

  const changeQuantity = (productId: string, change: number) => {
    setCart((current) => {
      const nextQuantity = (current[productId] || 0) + change
      const updated = { ...current }

      if (nextQuantity <= 0) {
        delete updated[productId]
      } else {
        updated[productId] = nextQuantity
      }

      return updated
    })
  }

  const openAuth = (
    mode: AuthMode = 'login',
    role: 'Buyer' | 'Seller' = 'Buyer',
  ) => {
    setAuthMode(mode)
    setAuthRole(role)
    setAuthError('')
    setAuthOpen(true)
    setPanel(null)
  }

  const logout = () => {
    sessionStorage.removeItem(TOKEN_KEY)
    setToken('')
    setUser(null)
    setOrders([])
    setPanel(null)
    setNotice('Вы вышли из аккаунта')
  }

  const submitAuth = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setAuthBusy(true)
    setAuthError('')

    try {
      const isRegister = authMode === 'register'

      const endpoint = isRegister
        ? '/api/Auth/register'
        : '/api/Auth/login'

      const body = isRegister
        ? {
            firstName: authForm.firstName,
            lastName: authForm.lastName,
            email: authForm.email,
            password: authForm.password,
            role: authRole,
          }
        : {
            email: authForm.email,
            password: authForm.password,
          }

      const response = await fetch(endpoint, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(body),
      })

      if (!response.ok) {
        throw new Error(await readError(response))
      }

      const data = await response.json()

      if (!data.accessToken) {
        throw new Error('Сервер не вернул токен авторизации')
      }

      sessionStorage.setItem(TOKEN_KEY, data.accessToken)
      setToken(data.accessToken)
      setAuthOpen(false)
      setAuthForm({
        firstName: '',
        lastName: '',
        email: '',
        password: '',
      })
      setNotice(
        isRegister
          ? 'Аккаунт успешно создан'
          : 'Вы успешно вошли в аккаунт',
      )
    } catch (err) {
      setAuthError(
        err instanceof Error ? err.message : 'Не удалось выполнить запрос',
      )
    } finally {
      setAuthBusy(false)
    }
  }

  const loadOrders = async () => {
  if (!token) return

  setOrdersLoading(true)
  setPanelError('')

  try {
    const response = await fetch('/api/Orders', {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    })

    if (!response.ok) {
      throw new Error(await readError(response))
    }

    const data = await response.json()
    const loadedOrders: Order[] = Array.isArray(data)
      ? data
      : data.items || []

    setOrders(loadedOrders)

    const paymentEntries = await Promise.all(
      loadedOrders.map(async (order) => {
        try {
          const paymentResponse = await fetch(
            `/api/Payments/${order.id}`,
            {
              headers: {
                Authorization: `Bearer ${token}`,
              },
            },
          )

          if (!paymentResponse.ok) return null

          const payment: PaymentInfo = await paymentResponse.json()
          return [order.id, payment] as const
        } catch {
          return null
        }
      }),
    )

    setPayments(
      Object.fromEntries(
        paymentEntries.filter(
          (entry): entry is readonly [string, PaymentInfo] =>
            entry !== null,
        ),
      ),
    )
  } catch (err) {
    setPanelError(
      err instanceof Error ? err.message : 'Не удалось загрузить заказы',
    )
  } finally {
    setOrdersLoading(false)
  }
}


const openSellerCabinet = () => {
  if (!user || !user.roles.includes('Seller')) return

  setPanelError('')
  setProductFormOpen(false)
  setEditingProduct(null)
  setPanel('seller')
}

const openNewProductForm = () => {
  setEditingProduct(null)
  setProductForm({
    name: '',
    description: '',
    price: '',
    currency: 'RUB',
    category: 'Смартфоны',
    imageUrl: '',
    stockQuantity: '1',
  })
  setPanelError('')
  setProductFormOpen(true)
}

const openEditProductForm = (product: Product) => {
  setEditingProduct(product)
  setProductForm({
    name: product.name,
    description: product.description || '',
    price: String(product.price),
    currency: product.currency,
    category: product.category,
    imageUrl: product.imageUrl || '',
    stockQuantity: String(stockByProduct[product.id] ?? 0),

  })
  setPanelError('')
  setProductFormOpen(true)
}

const submitProduct = async (event: React.FormEvent<HTMLFormElement>) => {
  event.preventDefault()

  if (!token || !user?.roles.includes('Seller')) return

  setProductBusy(true)
  setPanelError('')

  try {
    const body = {
      name: productForm.name.trim(),
      description: productForm.description.trim(),
      price: Number(productForm.price),
      currency: productForm.currency.trim().toUpperCase(),
      category: productForm.category.trim(),
      imageUrl: productForm.imageUrl.trim() || null,
      ...(editingProduct ? { isActive: true } : {}),
    }

    const response = await fetch(
      editingProduct
        ? `/api/Products/${editingProduct.id}`
        : '/api/Products',
      {
        method: editingProduct ? 'PUT' : 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify(body),
      },
    )

    if (!response.ok) {
      throw new Error(await readError(response))
    }

    const savedProduct: Product | null =
      response.status === 204 ? null : await response.json()

      const productId = savedProduct?.id ?? editingProduct?.id

      if (!productId) {
        throw new Error('Не удалось получить ID сохранённого товара')
      }

      const stockBody = {
        quantity: Number(productForm.stockQuantity),
      }

      if (
        !Number.isInteger(stockBody.quantity) ||
        stockBody.quantity < 0 ||
        stockBody.quantity > 1000000
      ) {
        throw new Error('Укажите корректное количество товара')
      }

      let stockResponse: Response

      if (editingProduct) {
        stockResponse = await fetch(`/api/inventory/${productId}`, {
          method: 'PUT',
          headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${token}`,
          },
          body: JSON.stringify(stockBody),
        })

        // Если остаток ещё не создавался, создаём его.
        if (stockResponse.status === 404) {
          stockResponse = await fetch('/api/inventory', {
            method: 'POST',
            headers: {
              'Content-Type': 'application/json',
              Authorization: `Bearer ${token}`,
            },
            body: JSON.stringify({
              productId,
              quantity: stockBody.quantity,
            }),
          })
        }
      } else {
        stockResponse = await fetch('/api/inventory', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${token}`,
          },
          body: JSON.stringify({
            productId,
            quantity: stockBody.quantity,
          }),
        })
      }

      if (!stockResponse.ok) {
        throw new Error(
          `Товар сохранён, но остаток не обновлён: ${await readError(stockResponse)}`,
        )
      }

      setStockByProduct((current) => ({
        ...current,
        [productId]: stockBody.quantity,
      }))

    if (savedProduct) {
      setProducts((current) => [
        ...current.filter((item) => item.id !== savedProduct.id),
        savedProduct,
      ])
    }

    setProductFormOpen(false)
    setEditingProduct(null)
    setNotice(editingProduct ? 'Товар обновлён' : 'Товар опубликован')
  } catch (err) {
    setPanelError(
      err instanceof Error ? err.message : 'Не удалось сохранить товар',
    )
  } finally {
    setProductBusy(false)
  }
}

const deleteProduct = async (product: Product) => {
  if (!token) return

  if (!window.confirm(`Удалить товар «${product.name}»?`)) return

  setProductBusy(true)
  setPanelError('')

  try {
    const response = await fetch(`/api/Products/${product.id}`, {
      method: 'DELETE',
      headers: {
        Authorization: `Bearer ${token}`,
      },
    })

    if (!response.ok) {
      throw new Error(await readError(response))
    }

    setProducts((current) =>
      current.filter((item) => item.id !== product.id),
    )
    setNotice('Товар удалён из каталога')
  } catch (err) {
    setPanelError(
      err instanceof Error ? err.message : 'Не удалось удалить товар',
    )
  } finally {
    setProductBusy(false)
  }
}


  const openOrders = async () => {
    if (!user) {
      openAuth('login')
      return
    }

    setPanel('orders')
    await loadOrders()
  }

  const checkout = async () => {
    if (!user || !token) {
      openAuth('login')
      return
    }

    if (user.roles && !user.roles.includes('Buyer')) {
      setPanelError('Оформлять заказы может аккаунт покупателя.')
      return
    }

    if (cartItems.length === 0) return

    setCheckoutBusy(true)
    setPanelError('')

    try {
      const response = await fetch('/api/Orders', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({
          items: cartItems.map((item) => ({
            productId: item.product.id,
            quantity: item.quantity,
          })),
        }),
      })

      if (!response.ok) {
        throw new Error(await readError(response))
      }

      const createdOrder = await response.json()

      setCart({})
      setPanel(null)
      setNotice(
        `Заказ создан. Номер: ${createdOrder.id || 'получен сервером'}`,
      )
      await loadOrders()
    } catch (err) {
      setPanelError(
        err instanceof Error ? err.message : 'Не удалось оформить заказ',
      )
    } finally {
      setCheckoutBusy(false)
    }
  }

  const getOrderStatus = (status?: string | number) => {
    if (status === 0 || status === 'Pending') return 'Ожидает обработки'
    if (status === 1 || status === 'AwaitingPayment') return 'Ожидает оплаты'
    if (status === 2 || status === 'Confirmed') return 'Подтверждён'
    if (status === 3 || status === 'Cancelled') return 'Отменён'
    return String(status ?? 'Статус неизвестен')
  }

  return (
    <div className="app">
      <div className="top-line">
        <div className="container top-line-inner">
          <span>✦ Всё для комфортной жизни</span>
          <span>Доставка по всей России</span>
        </div>
      </div>

      <header className="header">
        <div className="container header-inner">
          <a className="brand" href="#">
            <span className="brand-icon">M</span>
            <span>
              market<span className="brand-accent">flow</span>
            </span>
          </a>

          <div className="search-box">
            <span>⌕</span>
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Найти что-нибудь интересное..."
              aria-label="Поиск товаров"
            />
            <kbd>⌘ K</kbd>
          </div>


        <nav className="header-actions">
          {user?.roles?.includes('Seller') && (
            <button
              className="header-action"
              onClick={openSellerCabinet}
            >
              <span>▣</span>
              <small>Мои товары</small>
            </button>
          )}

          <button
            className="header-action"
            onClick={() => {
              if (user) setPanel('profile')
              else openAuth('login')
            }}
          >
            <span>♙</span>
            <small>
              {authChecking
                ? '...'
                : user
                  ? user.firstName || 'Профиль'
                  : 'Профиль'}
            </small>
          </button>

          <button
            className="header-action cart-action"
            onClick={() => {
              setPanelError('')
              setPanel('cart')
            }}
          >
            <span className="cart-icon">🛍</span>
            <small>Корзина</small>
            {cartCount > 0 && (
              <span className="cart-badge">{cartCount}</span>
            )}
          </button>
        </nav>

        </div>
      </header>

      <main>
        <section className="container hero">
          <div className="hero-content">
            <span className="eyebrow">
              <span className="eyebrow-dot" />
              МАРКЕТПЛЕЙС НОВОГО ПОКОЛЕНИЯ
            </span>

            <h1>
              Покупки,
              <br />
              которые <span>радуют.</span>
            </h1>

            <p>
              Находите любимые товары, открывайте новые бренды
              и покупайте с удовольствием.
            </p>

            <button
              className="primary-button"
              onClick={() =>
                document
                  .getElementById('catalog')
                  ?.scrollIntoView({ behavior: 'smooth' })
              }
            >
              Перейти к покупкам <span>→</span>
            </button>

            <div className="hero-benefits">
              <div>
                <strong>01</strong>
                <span>Удобный поиск</span>
              </div>
              <div>
                <strong>02</strong>
                <span>Хорошие цены</span>
              </div>
              <div>
                <strong>03</strong>
                <span>Простые покупки</span>
              </div>
            </div>
          </div>

          <div className="hero-visual">
            <div className="hero-orbit orbit-one" />
            <div className="hero-orbit orbit-two" />
            <div className="hero-product product-phone">📱</div>
            <div className="hero-product product-headphones">🎧</div>
            <div className="hero-product product-laptop">💻</div>
            <div className="floating-label label-top">
              <span>✦</span> Новинки уже здесь
            </div>
            <div className="floating-label label-bottom">
              <strong>Твой выбор.</strong>
              <span>Твои возможности.</span>
            </div>
          </div>
        </section>

        <section className="container advantages">
          <div className="advantage">
            <span className="advantage-icon">🚚</span>
            <div>
              <strong>Удобная доставка</strong>
              <small>Заказывайте из дома</small>
            </div>
          </div>

          <div className="advantage">
            <span className="advantage-icon">🛡️</span>
            <div>
              <strong>Безопасные покупки</strong>
              <small>Защита ваших заказов</small>
            </div>
          </div>

          <div className="advantage">
            <span className="advantage-icon">♡</span>
            <div>
              <strong>Любимые товары</strong>
              <small>Всё в одном месте</small>
            </div>
          </div>

          <div className="advantage">
            <span className="advantage-icon">↗</span>
            <div>
              <strong>Просто и удобно</strong>
              <small>Покупки без лишнего</small>
            </div>
          </div>
        </section>

        <section className="container catalog" id="catalog">
          <div className="section-heading">
            <div>
              <span className="section-kicker">ВЫБИРАЙТЕ СВОЁ</span>
              <h2>
                Популярные товары<span>.</span>
              </h2>
              <p>Подборка интересных товаров для вас</p>
            </div>

            <span className="products-count">
              {loading ? 'Загрузка...' : `${filteredProducts.length} товаров`}
            </span>
          </div>

          <div className="categories">
            {categories.map((category) => (
              <button
                key={category.name}
                className={
                  activeCategory === category.name
                    ? 'category-chip active'
                    : 'category-chip'
                }
                onClick={() => setActiveCategory(category.name)}
              >
                <span>{category.icon}</span>
                {category.name}
              </button>
            ))}
          </div>

          {loading && (
            <div className="empty-state">
              <h3>Загружаем товары...</h3>
              <p>Подождите немного, мы получаем данные из MarketFlow.</p>
            </div>
          )}

          {!loading && error && (
            <div className="empty-state">
              <h3>Не удалось загрузить товары</h3>
              <p>{error}</p>
              <button
                className="primary-button"
                onClick={() => window.location.reload()}
              >
                Попробовать снова
              </button>
            </div>
          )}

          {!loading && !error && (
            <>
              <div className="product-grid">
                {filteredProducts.map((product) => (
                  <article className="product-card" key={product.id}>
                    <div
                      className={`product-image ${getProductColor(product.category)}`}
                      onClick={() => setSelectedProduct(product)}
                      role="button"
                      tabIndex={0}
                      onKeyDown={(event) => {
                        if (event.key === 'Enter' || event.key === ' ') {
                          event.preventDefault()
                          setSelectedProduct(product)
                        }
                      }}
                    >
                      <button
                        className={
                          favorites.includes(product.id)
                            ? 'favorite-button selected'
                            : 'favorite-button'
                        }
                        onClick={() => toggleFavorite(product.id)}
                        aria-label="Добавить в избранное"
                      >
                        {favorites.includes(product.id) ? '♥' : '♡'}
                      </button>

                      {getProductImage(product) && (
                        <img
                          className="product-photo"
                          src={getProductImage(product)!}
                          alt={product.name}
                          loading="lazy"
                          onError={(event) => {
                            event.currentTarget.style.display = 'none'
                          }}
                        />
                      )}
                    </div>

                    <div className="product-info">
                      <span className="product-category">
                        {product.category}
                      </span>

                      <h3
                        onClick={() => setSelectedProduct(product)}
                        style={{ cursor: 'pointer' }}
                      >
                        {product.name}
                      </h3>

                      <p className="product-description">
                        {product.description}
                      </p>

                      <div className="product-bottom">
                        <div className="price-block">
                          <strong>
                            {formatPrice(product.price, product.currency)}
                          </strong>

                          <span className="product-stock">
                            {stockByProduct[product.id] > 0
                              ? `В наличии: ${stockByProduct[product.id]} шт.`
                              : 'Нет в наличии'}
                          </span>
                        </div>

                        <button
                          className="add-button"
                          onClick={() => addToCart(product)}
                          disabled={!stockByProduct[product.id]}
                          aria-label={`Добавить ${product.name} в корзину`}
                        >
                          +
                        </button>
                      </div>
                    </div>
                  </article>
                ))}
              </div>

              {filteredProducts.length === 0 && (
                <div className="empty-state">
                  <span>⌕</span>
                  <h3>Ничего не найдено</h3>
                  <p>
                    Попробуйте изменить запрос или выбрать другую категорию.
                  </p>
                  <button
                    className="primary-button"
                    onClick={() => {
                      setSearch('')
                      setActiveCategory('Все товары')
                    }}
                  >
                    Сбросить фильтры
                  </button>
                </div>
              )}
            </>
          )}
        </section>

        <section className="container seller-banner">
          <div>
            <span className="section-kicker">ЕСТЬ ЧТО ПРОДАТЬ?</span>
            <h2>Станьте частью MarketFlow.</h2>
            <p>
              Размещайте свои товары и находите новых покупателей.
            </p>
          </div>

          <button
            className="seller-button"
            onClick={() => openAuth('register', 'Seller')}
          >
            Стать продавцом <span>→</span>
          </button>
        </section>
      </main>

      <footer className="footer">
        <div className="container footer-inner">
          <a className="brand footer-brand" href="#">
            <span className="brand-icon">M</span>
            <span>
              market<span className="brand-accent">flow</span>
            </span>
          </a>

          <span>© 2026 MarketFlow. Учебный проект.</span>
          <span>Покупки становятся проще.</span>
        </div>
      </footer>


      {selectedProduct && (
        <div
          className="mf-overlay"
          onClick={() => setSelectedProduct(null)}
        >
          <section
            className="mf-modal mf-product-modal"
            onClick={(event) => event.stopPropagation()}
          >
            <button
              className="mf-close"
              onClick={() => setSelectedProduct(null)}
              aria-label="Закрыть"
            >
              ×
            </button>

            <div className="mf-product-details">
              <div
                className={`mf-product-preview ${getProductColor(selectedProduct.category)}`}
              >
                {getProductImage(selectedProduct) && (
                  <img
                    src={getProductImage(selectedProduct)!}
                    alt={selectedProduct.name}
                  />
                )}
              </div>

              <div className="mf-product-content">
                <span className="product-category">
                  {selectedProduct.category}
                </span>

                <h2>{selectedProduct.name}</h2>

                <p>{selectedProduct.description || 'Подробная информация о товаре.'}</p>

                <strong className="mf-product-price">
                  {formatPrice(
                    selectedProduct.price,
                    selectedProduct.currency
                  )}
                </strong>

                <button
                  className="primary-button"
                  onClick={() => {
                    addToCart(selectedProduct)
                    setSelectedProduct(null)
                  }}
                >
                  Добавить в корзину
                </button>
              </div>
            </div>
          </section>
        </div>
      )}


      {notice && (

        <div className="mf-toast">
          <span>{notice}</span>
          <button onClick={() => setNotice('')}>×</button>
        </div>
      )}

      {authOpen && (
        <div className="mf-overlay" onClick={() => setAuthOpen(false)}>
          <section
            className="mf-modal"
            onClick={(event) => event.stopPropagation()}
          >
            <button
              className="mf-close"
              onClick={() => setAuthOpen(false)}
              aria-label="Закрыть"
            >
              ×
            </button>

            <div className="mf-modal-brand">
              <span className="brand-icon">M</span>
              <span>
                market<span className="brand-accent">flow</span>
              </span>
            </div>

            <h2>
              {authMode === 'login'
                ? 'С возвращением!'
                : 'Создать аккаунт'}
            </h2>
            <p className="mf-subtitle">
              {authMode === 'login'
                ? 'Войдите, чтобы продолжить покупки.'
                : 'Присоединяйтесь к MarketFlow.'}
            </p>

            <form className="mf-form" onSubmit={submitAuth}>
              {authMode === 'register' && (
                <>
                  <label>
                    Имя
                    <input
                      required
                      value={authForm.firstName}
                      onChange={(event) =>
                        setAuthForm({
                          ...authForm,
                          firstName: event.target.value,
                        })
                      }
                      placeholder="Ваше имя"
                    />
                  </label>

                  <label>
                    Фамилия
                    <input
                      required
                      value={authForm.lastName}
                      onChange={(event) =>
                        setAuthForm({
                          ...authForm,
                          lastName: event.target.value,
                        })
                      }
                      placeholder="Ваша фамилия"
                    />
                  </label>

                  <label>
                    Тип аккаунта
                    <select
                      value={authRole}
                      onChange={(event) =>
                        setAuthRole(event.target.value as 'Buyer' | 'Seller')
                      }
                    >
                      <option value="Buyer">Покупатель</option>
                      <option value="Seller">Продавец</option>
                    </select>
                  </label>
                </>
              )}

              <label>
                Электронная почта
                <input
                  required
                  type="email"
                  value={authForm.email}
                  onChange={(event) =>
                    setAuthForm({
                      ...authForm,
                      email: event.target.value,
                    })
                  }
                  placeholder="name@example.com"
                />
              </label>

              <label>
                Пароль
                <input
                  required
                  type="password"
                  minLength={8}
                  value={authForm.password}
                  onChange={(event) =>
                    setAuthForm({
                      ...authForm,
                      password: event.target.value,
                    })
                  }
                  placeholder="Не менее 8 символов"
                />
              </label>

              {authError && <div className="mf-error">{authError}</div>}

              <button
                className="mf-submit"
                type="submit"
                disabled={authBusy}
              >
                {authBusy
                  ? 'Подождите...'
                  : authMode === 'login'
                    ? 'Войти'
                    : 'Зарегистрироваться'}
              </button>
            </form>

            <div className="mf-switch">
              {authMode === 'login' ? (
                <>
                  Нет аккаунта?{' '}
                  <button onClick={() => openAuth('register')}>
                    Зарегистрироваться
                  </button>
                </>
              ) : (
                <>
                  Уже есть аккаунт?{' '}
                  <button onClick={() => openAuth('login')}>
                    Войти
                  </button>
                </>
              )}
            </div>
          </section>
        </div>
      )}

      {panel && (
        <div className="mf-overlay" onClick={() => setPanel(null)}>
          <section
            className="mf-drawer"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="mf-drawer-header">
              <div>
                <span className="section-kicker">MARKETFLOW</span>
                <h2>
                  {panel === 'cart'
                    ? 'Корзина'
                    : panel === 'orders'
                      ? 'Мои заказы'
                      : 'Мой профиль'}
                </h2>
              </div>
              <button
                className="mf-close"
                onClick={() => setPanel(null)}
                aria-label="Закрыть"
              >
                ×
              </button>
            </div>

            {panelError && <div className="mf-error">{panelError}</div>}

            {panel === 'cart' && (
              <>
                {cartItems.length === 0 ? (
                  <div className="mf-empty">
                    <span>🛍</span>
                    <h3>Корзина пока пуста</h3>
                    <p>Добавьте товары из каталога, чтобы оформить заказ.</p>
                    <button
                      className="mf-submit"
                      onClick={() => {
                        setPanel(null)
                        document
                          .getElementById('catalog')
                          ?.scrollIntoView({ behavior: 'smooth' })
                      }}
                    >
                      Перейти в каталог
                    </button>
                  </div>
                ) : (
                  <>
                    <div className="mf-cart-list">
                      {cartItems.map(({ product, quantity }) => (
                        <article className="mf-cart-item" key={product.id}>
                          <div className="mf-cart-image">
                            {getProductImage(product) ? (
                              <img
                                src={getProductImage(product)!}
                                alt={product.name}
                              />
                            ) : (
                              <span>{getProductEmoji(product.category)}</span>
                            )}
                          </div>

                          <div className="mf-cart-details">
                            <strong>{product.name}</strong>
                            <small>
                              {formatPrice(product.price, product.currency)}
                            </small>

                            <div className="mf-quantity">
                              <button
                                onClick={() => changeQuantity(product.id, -1)}
                              >
                                −
                              </button>
                              <span>{quantity}</span>
                              <button
                                onClick={() => changeQuantity(product.id, 1)}
                              >
                                +
                              </button>
                              <button
                                className="mf-remove"
                                onClick={() =>
                                  setCart((current) => {
                                    const updated = { ...current }
                                    delete updated[product.id]
                                    return updated
                                  })
                                }
                              >
                                Удалить
                              </button>
                            </div>
                          </div>

                          <strong className="mf-line-total">
                            {formatPrice(
                              product.price * quantity,
                              product.currency,
                            )}
                          </strong>
                        </article>
                      ))}
                    </div>

                    <div className="mf-cart-summary">
                      <div>
                        <span>Товаров</span>
                        <strong>{cartCount}</strong>
                      </div>
                      <div className="mf-total">
                        <span>Итого</span>
                        <strong>
                          {formatPrice(
                            cartTotal,
                            cartItems[0]?.product.currency || 'RUB',
                          )}
                        </strong>
                      </div>
                      <button
                        className="mf-submit"
                        onClick={checkout}
                        disabled={checkoutBusy}
                      >
                        {checkoutBusy
                          ? 'Оформляем заказ...'
                          : 'Оформить заказ'}
                      </button>
                      <p className="mf-hint">
                        Для оформления заказа потребуется аккаунт покупателя.
                      </p>
                    </div>
                  </>
                )}
              </>
            )}

            {panel === 'profile' && (
              <div className="mf-profile">
                <div className="mf-avatar">
                  {(user?.firstName || user?.email || 'M')
                    .charAt(0)
                    .toUpperCase()}
                </div>
                <h3>
                  {user?.firstName} {user?.lastName}
                </h3>
                <p>{user?.email}</p>
                <span className="mf-role">
                  {user?.roles?.join(', ') || 'Пользователь'}
                </span>

                <button
                  className="mf-secondary"
                  onClick={openOrders}
                >
                  Мои заказы
                </button>
                <button className="mf-logout" onClick={logout}>
                  Выйти из аккаунта
                </button>
              </div>
            )}


            {panel === 'seller' && user?.roles?.includes('Seller') && (
              <div className="mf-seller">
                <div className="mf-seller-heading">
                  <div>
                    <h3>Мои товары</h3>
                    <p>Управляйте своими объявлениями</p>
                  </div>
                  {!productFormOpen && (
                    <button
                      className="mf-submit"
                      onClick={openNewProductForm}
                    >
                      + Добавить товар
                    </button>
                  )}
                </div>

                {productFormOpen && (
                  <form className="mf-product-form" onSubmit={submitProduct}>
                    <h3>
                      {editingProduct ? 'Редактировать товар' : 'Новое объявление'}
                    </h3>

                    <label>
                      Название товара
                      <input
                        required
                        minLength={2}
                        maxLength={150}
                        value={productForm.name}
                        onChange={(e) =>
                          setProductForm({ ...productForm, name: e.target.value })
                        }
                        placeholder="Например, iPhone 16 Pro"
                      />
                    </label>

                    <label>
                      Описание
                      <textarea
                        maxLength={2000}
                        value={productForm.description}
                        onChange={(e) =>
                          setProductForm({ ...productForm, description: e.target.value })
                        }
                        placeholder="Расскажите о товаре..."
                      />
                    </label>

                    <div className="mf-product-form-row">
                      <label>
                        Цена
                        <input
                          required
                          type="number"
                          min="0.01"
                          step="0.01"
                          value={productForm.price}
                          onChange={(e) =>
                            setProductForm({ ...productForm, price: e.target.value })
                          }
                          placeholder="Цена"
                        />
                      </label>

                      <label>
                        Валюта
                        <select
                          value={productForm.currency}
                          onChange={(e) =>
                            setProductForm({ ...productForm, currency: e.target.value })
                          }
                        >
                          <option value="RUB">RUB</option>
                          <option value="USD">USD</option>
                          <option value="EUR">EUR</option>
                        </select>
                      </label>
                    </div>

                    <label>
                      Категория
                      <select
                        value={productForm.category}
                        onChange={(e) =>
                          setProductForm({ ...productForm, category: e.target.value })
                        }
                      >
                        {categories.filter((item) => item.name !== 'Все товары').map((item) => (
                          <option key={item.name} value={item.name}>
                            {item.name}
                          </option>
                        ))}
                      </select>
                    </label>

                    <label>
                      Количество на складе
                      <input
                        required
                        type="number"
                        min={editingProduct ? stockByProduct[editingProduct.id] ?? 0 : 0}
                        max="1000000"
                        step="1"
                        value={productForm.stockQuantity}
                        onChange={(e) =>
                          setProductForm({
                            ...productForm,
                            stockQuantity: e.target.value,
                          })
                        }
                        placeholder="Количество"
                      />
                    </label>

                    <label>
                      Ссылка на фотографию
                      <input
                        type="url"
                        value={productForm.imageUrl}
                        onChange={(e) =>
                          setProductForm({ ...productForm, imageUrl: e.target.value })
                        }
                        placeholder="https://example.com/photo.jpg"
                      />
                    </label>

                    {productForm.imageUrl && (
                      <div className="mf-product-image-preview">
                        <img
                          src={productForm.imageUrl}
                          alt="Предпросмотр товара"
                          onError={(e) => {
                            e.currentTarget.style.display = 'none'
                          }}
                          onLoad={(e) => {
                            e.currentTarget.style.display = 'block'
                          }}
                        />
                      </div>
                    )}

                    <div className="mf-product-form-actions">
                      <button
                        className="mf-submit"
                        type="submit"
                        disabled={productBusy}
                      >
                        {productBusy
                          ? 'Сохраняем...'
                          : editingProduct
                            ? 'Сохранить изменения'
                            : 'Опубликовать'}
                      </button>

                      <button
                        className="mf-secondary"
                        type="button"
                        onClick={() => {
                          setProductFormOpen(false)
                          setEditingProduct(null)
                          setPanelError('')
                        }}
                      >
                        Отмена
                      </button>
                    </div>
                  </form>
                )}

                {!productFormOpen && (
                  <div className="mf-seller-products">
                    {products.filter((product) => product.sellerId === user.id).length === 0 ? (
                      <div className="mf-empty">
                        <span>📦</span>
                        <h3>У вас пока нет товаров</h3>
                        <p>Добавьте первое объявление — оно появится в общем каталоге.</p>
                      </div>
                    ) : (
                      products
                        .filter((product) => product.sellerId === user.id)
                        .map((product) => (
                          <article className="mf-seller-product" key={product.id}>
                            <div className="mf-seller-product-image">
                              {getProductImage(product) ? (
                                <img src={getProductImage(product)!} alt={product.name} />
                              ) : (
                                <span>{getProductEmoji(product.category)}</span>
                              )}
                            </div>

                            <div className="mf-seller-product-info">
                              <strong>{product.name}</strong>
                              <span>{formatPrice(product.price, product.currency)}</span>
                              <small>{product.category}</small>
                            </div>

                            <div className="mf-seller-product-actions">
                              <button
                                className="mf-secondary"
                                onClick={() => openEditProductForm(product)}
                              >
                                Изменить
                              </button>
                              <button
                                className="mf-logout"
                                disabled={productBusy}
                                onClick={() => deleteProduct(product)}
                              >
                                Удалить
                              </button>
                            </div>
                          </article>
                        ))
                    )}
                  </div>
                )}
              </div>
            )}


            {panel === 'orders' && (
              <div className="mf-orders">
                {ordersLoading ? (
                  <div className="mf-empty">
                    Загружаем историю заказов...
                  </div>
                ) : orders.length === 0 ? (
                  <div className="mf-empty">
                    <span>📦</span>
                    <h3>Заказов пока нет</h3>
                    <p>Здесь будут отображаться ваши покупки.</p>
                  </div>
                ) : (
                  orders.map((order) => (

                  <article className="mf-order" key={order.id}>
                    <div className="mf-order-top">
                      <div>
                        <strong>Заказ #{order.id.slice(0, 8)}</strong>
                        <small>
                          {order.createdAt
                            ? new Date(order.createdAt).toLocaleString('ru-RU')
                            : 'Дата не указана'}
                        </small>
                      </div>

                      <span
                        className={`mf-order-status ${
                          order.status === 2 || order.status === 'Confirmed'
                            ? 'confirmed'
                            : order.status === 3 || order.status === 'Cancelled'
                              ? 'cancelled'
                              : 'pending'
                        }`}
                      >
                        {getOrderStatus(order.status)}
                      </span>
                    </div>

                    {typeof order.totalAmount === 'number' && (
                      <strong className="mf-order-total">
                        {formatPrice(order.totalAmount, order.currency || 'RUB')}
                      </strong>
                    )}

                    {payments[order.id] && (
                      <div className="mf-payment-info">
                        <strong>Оплата</strong>

                        <span>
                          Статус: {
                            payments[order.id].status === 1 ||
                            payments[order.id].status === 'Succeeded'
                              ? 'Успешно'
                              : payments[order.id].status === 2 ||
                                payments[order.id].status === 'Failed'
                                ? 'Неуспешно'
                                : String(payments[order.id].status)
                          }
                        </span>

                        <span>
                          Сумма: {formatPrice(
                            payments[order.id].amount,
                            payments[order.id].currency
                          )}
                        </span>

                        {payments[order.id].completedAt && (
                          <span>
                            Завершено: {
                              new Date(payments[order.id].completedAt!).toLocaleString('ru-RU')
                            }
                          </span>
                        )}

                        {payments[order.id].failureReason && (
                          <span>Причина: {payments[order.id].failureReason}</span>
                        )}
                      </div>
                    )}

                    {order.status === 3 || order.status === 'Cancelled' ? (
                      <div className="mf-order-cancelled">
                        <span>×</span>
                        Заказ отменён
                      </div>
                    ) : (
                      <div className="mf-order-progress">
                        <div
                          className={`mf-order-progress-line ${
                            order.status === 2 || order.status === 'Confirmed'
                              ? 'completed'
                              : ''
                          }`}
                        />
                        <div className="mf-order-step">
                          <span className="done">✓</span>
                          <small>Создан</small>
                        </div>
                        <div className="mf-order-step">
                          <span
                            className={
                              order.status === 2 || order.status === 'Confirmed'
                                ? 'done'
                                : 'active'
                            }
                          >
                            {order.status === 2 || order.status === 'Confirmed' ? '✓' : '2'}
                          </span>
                          <small>Обработка</small>
                        </div>
                        <div className="mf-order-step">
                          <span
                            className={
                              order.status === 2 || order.status === 'Confirmed'
                                ? 'done'
                                : ''
                            }
                          >
                            {order.status === 2 || order.status === 'Confirmed' ? '✓' : '3'}
                          </span>
                          <small>Подтверждение</small>
                        </div>
                      </div>
                    )}
                  </article>

                  ))
                )}
                <button className="mf-secondary" onClick={loadOrders}>
                  Обновить список
                </button>
              </div>
            )}
          </section>
        </div>
      )}
    </div>
  )
}

export default App
