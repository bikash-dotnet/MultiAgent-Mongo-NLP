'use strict';

const documents = [
  {
    _id: 'u1001',
    name: 'Aarav',
    city: 'Pune',
    age: 29,
    status: 'active',
    signupDate: '2023-04-11T00:00:00Z',
    orders: [
      { orderId: 'o1001', amount: 1200, ts: '2023-05-01T00:00:00Z' },
      { orderId: 'o1002', amount: 800, ts: '2023-06-01T00:00:00Z' },
    ],
  },
  {
    _id: 'u1002',
    name: 'Diya',
    city: 'Pune',
    age: 34,
    status: 'active',
    signupDate: '2022-11-02T00:00:00Z',
    orders: [
      { orderId: 'o1003', amount: 1750, ts: '2023-03-14T00:00:00Z' },
    ],
  },
  {
    _id: 'u1003',
    name: 'Ishaan',
    city: 'Mumbai',
    age: 41,
    status: 'active',
    signupDate: '2021-07-19T00:00:00Z',
    orders: [
      { orderId: 'o1004', amount: 990, ts: '2023-02-09T00:00:00Z' },
      { orderId: 'o1005', amount: 150, ts: '2023-04-28T00:00:00Z' },
    ],
  },
  {
    _id: 'u1004',
    name: 'Meera',
    city: 'Pune',
    age: 26,
    status: 'paused',
    signupDate: '2023-08-30T00:00:00Z',
    orders: [
      { orderId: 'o1006', amount: 420, ts: '2023-09-05T00:00:00Z' },
    ],
  },
  {
    _id: 'u1005',
    name: 'Kabir',
    city: 'Delhi',
    age: 38,
    status: 'active',
    signupDate: '2022-01-22T00:00:00Z',
    orders: [
      { orderId: 'o1007', amount: 640, ts: '2023-01-30T00:00:00Z' },
      { orderId: 'o1008', amount: 2100, ts: '2023-07-11T00:00:00Z' },
    ],
  },
  {
    _id: 'u1006',
    name: 'Anaya',
    city: 'Pune',
    age: 31,
    status: 'active',
    signupDate: '2022-05-16T00:00:00Z',
    orders: [
      { orderId: 'o1009', amount: 300, ts: '2023-03-01T00:00:00Z' },
      { orderId: 'o1010', amount: 560, ts: '2023-05-19T00:00:00Z' },
    ],
  },
];

const flattenedSchema = {
  collection: 'shop.users',
  documentCount: documents.length,
  fields: {
    _id: { type: 'objectId' },
    name: { type: 'string' },
    city: { type: 'string', enum: ['Pune', 'Mumbai', 'Delhi'] },
    age: { type: 'int' },
    status: { type: 'string', enum: ['active', 'paused'] },
    signupDate: { type: 'date' },
    'orders.orderId': { type: 'string', array: true },
    'orders.amount': { type: 'decimal', array: true },
    'orders.ts': { type: 'date', array: true },
  },
  arrays: {
    orders: { correlated: true, elementFields: ['orderId', 'amount', 'ts'] },
  },
};

const compactSchema = {
  coll: 'shop.users',
  f: {
    _id: 'oid',
    name: 'str',
    city: { enum: ['Pune', 'Mumbai', 'Delhi'] },
    age: 'int',
    status: { enum: ['active', 'paused'] },
    signupDate: 'date',
    orders: {
      '[]': { orderId: 'str', amount: 'dec', ts: 'date' },
      corr: 1,
    },
  },
};

const pipeline = [
  { $match: { city: 'Pune' } },
  { $unwind: '$orders' },
  {
    $group: {
      _id: '$name',
      totalAmount: { $sum: '$orders.amount' },
      orderCount: { $sum: 1 },
    },
  },
  { $sort: { totalAmount: -1 } },
  { $limit: 5 },
  { $project: { _id: 0, name: '$_id', totalAmount: 1, orderCount: 1 } },
];

const warnings = [
  'Demo mode: pipeline and results are sample data, not executed against MongoDB.',
];

const results = [
  { name: 'Aarav', totalAmount: 2000, orderCount: 2 },
  { name: 'Diya', totalAmount: 1750, orderCount: 1 },
  { name: 'Anaya', totalAmount: 860, orderCount: 2 },
  { name: 'Meera', totalAmount: 420, orderCount: 1 },
];

module.exports = {
  documents,
  flattenedSchema,
  compactSchema,
  pipeline,
  warnings,
  results,
};
